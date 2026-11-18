using System.Net;
using System.Net.Http.Json;
using Opsynex.Application.Auth;
using Opsynex.Domain.Enums;
using Opsynex.IntegrationTests.Infrastructure;

namespace Opsynex.IntegrationTests.Auth;

public sealed class UsersEndpointsTests(OpsynexApiFactory factory) : IClassFixture<OpsynexApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DatabaseFixture Db => factory.Database;

    [Fact]
    public async Task Admin_ListsUsersFilteredByCompanyAndRole()
    {
        var companyId = await TestUsers.CreateCompanyAsync(Db, "Empresa Listado");
        var analyst = await TestUsers.CreateUserAsync(Db, UserRole.Analyst, companyId);
        var client = await TestUsers.CreateUserAsync(Db, UserRole.Client, companyId);
        await TestUsers.CreateUserAsync(Db, UserRole.Client, await TestUsers.CreateCompanyAsync(Db));
        var (admin, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        using (admin)
        {
            var users = await (await admin.GetAsync($"/api/v1/users?companyId={companyId}", Ct)).ReadAsync<List<UserDto>>();
            users.Select(u => u.Id).ShouldBe([analyst.Id, client.Id], ignoreOrder: true);
            users.ShouldAllBe(u => u.CompanyName == "Empresa Listado");

            var clients = await (await admin.GetAsync($"/api/v1/users?companyId={companyId}&role=Client", Ct)).ReadAsync<List<UserDto>>();
            clients.ShouldHaveSingleItem().Id.ShouldBe(client.Id);

            var bySearch = await (await admin.GetAsync($"/api/v1/users?search={analyst.Email[..20]}", Ct)).ReadAsync<List<UserDto>>();
            bySearch.ShouldHaveSingleItem().Id.ShouldBe(analyst.Id);
        }
    }

    [Fact]
    public async Task Admin_CreatesGetsAndUpdatesUser()
    {
        var companyA = await TestUsers.CreateCompanyAsync(Db);
        var companyB = await TestUsers.CreateCompanyAsync(Db);
        var (admin, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        using (admin)
        {
            var email = $"luis-{Guid.NewGuid():N}@opsynex.test";
            using var create = await admin.PostAsJsonAsync(
                "/api/v1/users", new { email, password = "Clave2026a", fullName = "Luis", role = "Client", companyId = companyA }, Ct);
            var created = await create.ReadAsync<UserDto>(HttpStatusCode.Created);

            (await (await admin.GetAsync($"/api/v1/users/{created.Id}", Ct)).ReadAsync<UserDto>()).Email.ShouldBe(email);

            using var update = await admin.PutAsJsonAsync(
                $"/api/v1/users/{created.Id}",
                new { email, fullName = "Luis P.", role = "Analyst", companyId = companyB, isActive = true },
                Ct);
            var updated = await update.ReadAsync<UserDto>();
            updated.FullName.ShouldBe("Luis P.");
            updated.Role.ShouldBe(UserRole.Analyst);
            updated.CompanyId.ShouldBe(companyB);
        }
    }

    [Fact]
    public async Task Update_EmailTakenByOtherUser_Returns409()
    {
        var companyId = await TestUsers.CreateCompanyAsync(Db);
        var first = await TestUsers.CreateUserAsync(Db, UserRole.Client, companyId);
        var second = await TestUsers.CreateUserAsync(Db, UserRole.Client, companyId);
        var (admin, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        using (admin)
        {
            using var response = await admin.PutAsJsonAsync(
                $"/api/v1/users/{second.Id}",
                new { email = first.Email, fullName = "X", role = "Client", companyId, isActive = true },
                Ct);

            await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "user.email_taken");
        }
    }

    [Fact]
    public async Task Deactivate_PreventsLogin()
    {
        var user = await TestUsers.CreateUserAsync(Db, UserRole.Analyst, await TestUsers.CreateCompanyAsync(Db));
        var (admin, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        using (admin)
        {
            (await admin.DeleteAsync($"/api/v1/users/{user.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
            (await (await admin.GetAsync($"/api/v1/users/{user.Id}", Ct)).ReadAsync<UserDto>()).IsActive.ShouldBeFalse();
        }

        using var anonymous = factory.CreateClient();
        using var login = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email = user.Email, password = user.Password }, Ct);
        await login.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.user_inactive");
    }

    [Fact]
    public async Task Admin_CannotDeactivateOrDemoteSelf()
    {
        var (admin, me) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        using (admin)
        {
            using var delete = await admin.DeleteAsync($"/api/v1/users/{me.Id}", Ct);
            await delete.ShouldBeProblemAsync(HttpStatusCode.Conflict, "user.self_modification");

            using var demote = await admin.PutAsJsonAsync(
                $"/api/v1/users/{me.Id}",
                new { email = me.Email, fullName = "Yo", role = "Analyst", companyId = await TestUsers.CreateCompanyAsync(Db), isActive = true },
                Ct);
            await demote.ShouldBeProblemAsync(HttpStatusCode.Conflict, "user.self_modification");
        }
    }

    [Fact]
    public async Task SetPassword_WeakPassword_Returns400()
    {
        var user = await TestUsers.CreateUserAsync(Db, UserRole.Client, await TestUsers.CreateCompanyAsync(Db));
        var (admin, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        using (admin)
        {
            using var response = await admin.PutAsJsonAsync($"/api/v1/users/{user.Id}/password", new { newPassword = "12345678" }, Ct);

            var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
            problem.GetProperty("errors").GetProperty("newPassword")[0].GetString()!.ShouldContain("letra");
        }
    }

    [Fact]
    public async Task UnknownUser_Returns404()
    {
        var (admin, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        using (admin)
        {
            using var response = await admin.GetAsync($"/api/v1/users/{Guid.NewGuid()}", Ct);

            await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
        }
    }

    [Theory]
    [InlineData(UserRole.Analyst)]
    [InlineData(UserRole.Client)]
    public async Task NonAdmin_CannotUseUserEndpoints(UserRole role)
    {
        var (client, user) = await TestUsers.CreateAuthenticatedClientAsync(factory, role);
        using (client)
        {
            await (await client.GetAsync("/api/v1/users", Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
            await (await client.GetAsync($"/api/v1/users/{user.Id}", Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
            await (await client.DeleteAsync($"/api/v1/users/{user.Id}", Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
            await (await client.PutAsJsonAsync($"/api/v1/users/{user.Id}/password", new { newPassword = "Nueva2026x" }, Ct))
                .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
        }
    }
}
