using System.Net;
using System.Net.Http.Json;
using Opsynex.Domain.Enums;
using Opsynex.IntegrationTests.Infrastructure;

namespace Opsynex.IntegrationTests.Auth;

/// <summary>Bloqueo temporal tras 5 intentos fallidos (con reloj controlable).</summary>
public sealed class LockoutTests(TimeTravelApiFactory factory) : IClassFixture<TimeTravelApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FiveFailedAttempts_LockAccountUntilLockoutEnds()
    {
        var user = await TestUsers.CreateUserAsync(factory.Database, UserRole.Analyst, await TestUsers.CreateCompanyAsync(factory.Database));
        using var client = factory.CreateClient();

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            using var failed = await Login(client, user.Email, "Incorrecta1");
            await failed.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        }

        using (var fifth = await Login(client, user.Email, "Incorrecta1"))
        {
            await fifth.ShouldBeProblemAsync(HttpStatusCode.Locked, "auth.locked_out");
            fifth.Headers.RetryAfter!.Delta.ShouldBe(TimeSpan.FromMinutes(15));
        }

        // Bloqueada incluso con la contraseña correcta.
        factory.Clock.Advance(TimeSpan.FromMinutes(14));
        using (var whileLocked = await Login(client, user.Email, user.Password))
        {
            await whileLocked.ShouldBeProblemAsync(HttpStatusCode.Locked, "auth.locked_out");
        }

        factory.Clock.Advance(TimeSpan.FromMinutes(2));
        using var afterLockout = await Login(client, user.Email, user.Password);
        afterLockout.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SuccessfulLogin_ResetsFailedCounter()
    {
        var user = await TestUsers.CreateUserAsync(factory.Database, UserRole.Client, await TestUsers.CreateCompanyAsync(factory.Database));
        using var client = factory.CreateClient();

        for (var i = 0; i < 4; i++)
        {
            (await Login(client, user.Email, "Incorrecta1")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        (await Login(client, user.Email, user.Password)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // El contador volvió a cero: 4 fallos más no bloquean.
        for (var i = 0; i < 4; i++)
        {
            (await Login(client, user.Email, "Incorrecta1")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }

    [Fact]
    public async Task AdminPasswordReset_UnlocksAccount()
    {
        var user = await TestUsers.CreateUserAsync(factory.Database, UserRole.Client, await TestUsers.CreateCompanyAsync(factory.Database));
        using var client = factory.CreateClient();
        for (var i = 0; i < 5; i++)
        {
            await Login(client, user.Email, "Incorrecta1");
        }

        var (admin, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        using (admin)
        {
            using var reset = await admin.PutAsJsonAsync($"/api/v1/users/{user.Id}/password", new { newPassword = "Nueva2026x" }, Ct);
            reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await Login(client, user.Email, user.Password)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Login(client, user.Email, "Nueva2026x")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Token_ExpiresAfterConfiguredMinutes()
    {
        var (client, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Client);
        using (client)
        {
            (await client.GetAsync("/api/v1/auth/me", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

            factory.Clock.Advance(TimeSpan.FromMinutes(61));

            using var response = await client.GetAsync("/api/v1/auth/me", Ct);
            await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.token_expired");
        }
    }

    private static Task<HttpResponseMessage> Login(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, Ct);
}
