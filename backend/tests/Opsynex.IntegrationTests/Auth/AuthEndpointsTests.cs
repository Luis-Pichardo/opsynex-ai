using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Opsynex.Application.Auth;
using Opsynex.Domain.Enums;
using Opsynex.Infrastructure.Security;
using Opsynex.IntegrationTests.Infrastructure;

namespace Opsynex.IntegrationTests.Auth;

public sealed class AuthEndpointsTests(OpsynexApiFactory factory) : IClassFixture<OpsynexApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DatabaseFixture Db => factory.Database;

    [Fact]
    public async Task Login_ReturnsJwtWithExpectedClaimsAndNoPassword()
    {
        var companyId = await TestUsers.CreateCompanyAsync(Db, "Empresa Login");
        var user = await TestUsers.CreateUserAsync(Db, UserRole.Analyst, companyId);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email = user.Email.ToUpperInvariant(), password = user.Password }, Ct);

        var raw = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, raw);
        raw.ShouldNotContain(user.Password);
        raw.ShouldNotContain("password", Case.Insensitive);

        var body = JsonSerializer.Deserialize<LoginResponse>(raw, Json.Options)!;
        body.TokenType.ShouldBe("Bearer");
        body.User.Email.ShouldBe(user.Email);
        body.User.Role.ShouldBe(UserRole.Analyst);
        body.User.CompanyName.ShouldBe("Empresa Login");
        body.User.LastLoginAt.ShouldNotBeNull();

        var jwt = new JsonWebToken(body.AccessToken);
        jwt.GetClaim("sub").Value.ShouldBe(user.Id.ToString());
        jwt.GetClaim("email").Value.ShouldBe(user.Email);
        jwt.GetClaim("role").Value.ShouldBe("Analyst");
        jwt.GetClaim("company_id").Value.ShouldBe(companyId.ToString());
        (jwt.ValidTo - jwt.IssuedAt).ShouldBe(TimeSpan.FromMinutes(60));
        body.ExpiresAt.ShouldBe(jwt.ValidTo, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Login_GlobalAdmin_HasNoCompanyClaim()
    {
        var admin = await TestUsers.CreateUserAsync(Db, UserRole.Admin, companyId: null);
        using var client = factory.CreateClient();

        var token = await TestUsers.LoginAsync(client, admin.Email, admin.Password);

        new JsonWebToken(token).TryGetClaim("company_id", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Login_WrongPasswordAndUnknownEmail_ReturnSameError()
    {
        var user = await TestUsers.CreateUserAsync(Db, UserRole.Client, await TestUsers.CreateCompanyAsync(Db));
        using var client = factory.CreateClient();

        using var wrongPassword = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = "Incorrecta1" }, Ct);
        using var unknownEmail = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "nadie@opsynex.test", password = "Incorrecta1" }, Ct);

        var a = await wrongPassword.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        var b = await unknownEmail.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        a.GetProperty("detail").GetString().ShouldBe(b.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Login_InactiveUser_Returns403()
    {
        var user = await TestUsers.CreateUserAsync(Db, UserRole.Client, await TestUsers.CreateCompanyAsync(Db), isActive: false);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = user.Password }, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.user_inactive");
    }

    [Fact]
    public async Task Login_EmptyFields_Returns400WithErrors()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "", password = "" }, Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        problem.GetProperty("errors").TryGetProperty("email", out _).ShouldBeTrue();
        problem.GetProperty("errors").TryGetProperty("password", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Me_WithToken_ReturnsCurrentUser()
    {
        var (client, user) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Client);
        using (client)
        {
            var me = await (await client.GetAsync("/api/v1/auth/me", Ct)).ReadAsync<UserDto>();

            me.Id.ShouldBe(user.Id);
            me.Role.ShouldBe(UserRole.Client);
            me.CompanyId.ShouldBe(user.CompanyId);
        }
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/auth/me", Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
        response.Headers.WwwAuthenticate.ToString().ShouldStartWith("Bearer");
    }

    [Theory]
    [InlineData("no-es-un-jwt")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.firma-falsa")]
    public async Task Me_WithMalformedToken_Returns401(string token)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync("/api/v1/auth/me", Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_token");
    }

    [Fact]
    public async Task Me_WithTokenSignedByOtherKey_Returns401()
    {
        var user = await TestUsers.CreateUserAsync(Db, UserRole.Admin, null);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken(user, DateTime.UtcNow.AddMinutes(30), "otra-clave-distinta-de-al-menos-32-bytes-xx"));

        using var response = await client.GetAsync("/api/v1/auth/me", Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_token");
    }

    [Fact]
    public async Task Me_WithExpiredToken_Returns401TokenExpired()
    {
        var user = await TestUsers.CreateUserAsync(Db, UserRole.Admin, null);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken(user, DateTime.UtcNow.AddMinutes(-5), TestJwt.Secret));

        using var response = await client.GetAsync("/api/v1/auth/me", Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.token_expired");
    }

    [Fact]
    public async Task Me_AfterUserIsDeactivatedOrRoleChanges_Returns401()
    {
        var (client, user) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst);
        using (client)
        {
            (await client.GetAsync("/api/v1/auth/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

            await using (var context = Db.CreateContext())
            {
                var entity = await context.Users.SingleAsync(u => u.Id == user.Id, Ct);
                entity.Role = UserRole.Client;
                await context.SaveChangesAsync(Ct);
            }

            using var response = await client.GetAsync("/api/v1/auth/me", Ct);
            await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.session_invalid");
        }
    }

    [Fact]
    public async Task Register_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", NewUserBody(UserRole.Client, Guid.NewGuid()), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }

    [Theory]
    [InlineData(UserRole.Analyst)]
    [InlineData(UserRole.Client)]
    public async Task Register_ByNonAdmin_Returns403(UserRole role)
    {
        var (client, user) = await TestUsers.CreateAuthenticatedClientAsync(factory, role);
        using (client)
        {
            using var response = await client.PostAsJsonAsync("/api/v1/auth/register", NewUserBody(UserRole.Client, user.CompanyId), Ct);

            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
        }
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Analyst)]
    [InlineData(UserRole.Client)]
    public async Task Register_ByAdmin_CreatesUserWithHashedPassword(UserRole role)
    {
        var companyId = await TestUsers.CreateCompanyAsync(Db);
        var (admin, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        var body = NewUserBody(role, companyId);
        using (admin)
        {
            using var response = await admin.PostAsJsonAsync("/api/v1/auth/register", body, Ct);

            var raw = await response.Content.ReadAsStringAsync(Ct);
            response.StatusCode.ShouldBe(HttpStatusCode.Created, raw);
            response.Headers.Location.ShouldNotBeNull();
            raw.ShouldNotContain(body.password);
            raw.ShouldNotContain("passwordHash", Case.Insensitive);

            var created = JsonSerializer.Deserialize<UserDto>(raw, Json.Options)!;
            created.Role.ShouldBe(role);
            created.CompanyId.ShouldBe(companyId);

            await using var context = Db.CreateContext();
            var stored = await context.Users.SingleAsync(u => u.Id == created.Id, Ct);
            stored.PasswordHash.ShouldNotBe(body.password);
            stored.PasswordHash.ShouldNotContain(body.password);
            new PasswordService().Verify(stored, stored.PasswordHash, body.password)
                .ShouldBe(Opsynex.Application.Abstractions.Security.PasswordVerification.Success);

            // El usuario creado puede iniciar sesión.
            using var anonymous = factory.CreateClient();
            (await TestUsers.LoginAsync(anonymous, body.email, body.password)).ShouldNotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task Register_InvalidData_Returns400WithFieldErrors()
    {
        var (admin, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        using (admin)
        {
            using var response = await admin.PostAsJsonAsync(
                "/api/v1/auth/register",
                new { email = "no-es-correo", password = "sololetras", fullName = "", role = "Analyst" },
                Ct);

            var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
            var errors = problem.GetProperty("errors");
            errors.GetProperty("email").GetArrayLength().ShouldBe(1);
            errors.GetProperty("password")[0].GetString()!.ShouldContain("número");
            errors.TryGetProperty("fullName", out _).ShouldBeTrue();
            errors.TryGetProperty("companyId", out _).ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var companyId = await TestUsers.CreateCompanyAsync(Db);
        var existing = await TestUsers.CreateUserAsync(Db, UserRole.Client, companyId);
        var (admin, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        using (admin)
        {
            var body = NewUserBody(UserRole.Client, companyId) with { email = existing.Email.ToUpperInvariant() };

            using var response = await admin.PostAsJsonAsync("/api/v1/auth/register", body, Ct);

            await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "user.email_taken");
        }
    }

    [Fact]
    public async Task Register_UnknownCompany_Returns400()
    {
        var (admin, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        using (admin)
        {
            using var response = await admin.PostAsJsonAsync("/api/v1/auth/register", NewUserBody(UserRole.Analyst, Guid.NewGuid()), Ct);

            await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "user.company_not_found");
        }
    }

    private static NewUser NewUserBody(UserRole role, Guid? companyId) =>
        new($"nuevo-{Guid.NewGuid():N}@opsynex.test", "Segura2026", "Usuario Nuevo", role.ToString(), companyId);

    private sealed record NewUser(string email, string password, string fullName, string role, Guid? companyId);

    private static string CreateToken(TestUser user, DateTime expires, string secret) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", user.Id.ToString()),
                new Claim("email", user.Email),
                new Claim("role", user.Role.ToString()),
            ]),
            Issuer = "opsynex-api",
            Audience = "opsynex-web",
            IssuedAt = expires.AddMinutes(-60),
            NotBefore = expires.AddMinutes(-60),
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256),
        });
}
