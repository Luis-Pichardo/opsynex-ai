using System.Net;
using System.Net.Http.Json;
using Opsynex.Application.Companies;
using Opsynex.Domain.Enums;
using Opsynex.IntegrationTests.Infrastructure;

namespace Opsynex.IntegrationTests.Auth;

public sealed class CompaniesEndpointsTests(OpsynexApiFactory factory) : IClassFixture<OpsynexApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Admin_CanCreateReadUpdateAndDeleteCompany()
    {
        var (admin, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        using (admin)
        {
            using var create = await admin.PostAsJsonAsync(
                "/api/v1/companies", new { name = "  Metalúrgica Norte ", industry = "Manufacturing", sector = "Metalmecánica" }, Ct);
            var created = await create.ReadAsync<CompanyDto>(HttpStatusCode.Created);
            created.Name.ShouldBe("Metalúrgica Norte");
            created.Currency.ShouldBe("USD");
            create.Headers.Location!.ToString().ShouldEndWith($"/api/v1/companies/{created.Id}");

            var fetched = await (await admin.GetAsync($"/api/v1/companies/{created.Id}", Ct)).ReadAsync<CompanyDto>();
            fetched.Sector.ShouldBe("Metalmecánica");

            using var update = await admin.PutAsJsonAsync(
                $"/api/v1/companies/{created.Id}", new { name = "Metalúrgica Sur", industry = "Services", currency = "DOP" }, Ct);
            var updated = await update.ReadAsync<CompanyDto>();
            updated.Name.ShouldBe("Metalúrgica Sur");
            updated.Industry.ShouldBe(Industry.Services);
            updated.Currency.ShouldBe("DOP");
            updated.Sector.ShouldBeNull();

            (await admin.DeleteAsync($"/api/v1/companies/{created.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
            using var afterDelete = await admin.GetAsync($"/api/v1/companies/{created.Id}", Ct);
            await afterDelete.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
        }
    }

    [Theory]
    [InlineData("""{"name":"","industry":"Manufacturing"}""", "name")]
    [InlineData("""{"name":"X","industry":"Manufacturing","currency":"usd"}""", "currency")]
    [InlineData("""{"name":"X","industry":"Manufacturing","currency":"DOLAR"}""", "currency")]
    public async Task Create_InvalidData_Returns400(string json, string field)
    {
        var (admin, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        using (admin)
        {
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            using var response = await admin.PostAsync("/api/v1/companies", content, Ct);

            var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
            problem.GetProperty("errors").TryGetProperty(field, out _).ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Delete_CompanyWithUsers_Returns409()
    {
        var companyId = await TestUsers.CreateCompanyAsync(factory.Database);
        await TestUsers.CreateUserAsync(factory.Database, UserRole.Client, companyId);
        var (admin, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        using (admin)
        {
            using var response = await admin.DeleteAsync($"/api/v1/companies/{companyId}", Ct);

            await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "company.in_use");
        }
    }

    [Theory]
    [InlineData(UserRole.Analyst)]
    [InlineData(UserRole.Client)]
    public async Task NonAdmin_SeesOnlyOwnCompany_AndOtherCompanyIs404(UserRole role)
    {
        var otherCompany = await TestUsers.CreateCompanyAsync(factory.Database);
        var (client, user) = await TestUsers.CreateAuthenticatedClientAsync(factory, role);
        using (client)
        {
            var list = await (await client.GetAsync("/api/v1/companies", Ct)).ReadAsync<List<CompanyDto>>();
            list.ShouldHaveSingleItem().Id.ShouldBe(user.CompanyId!.Value);

            (await client.GetAsync($"/api/v1/companies/{user.CompanyId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

            using var other = await client.GetAsync($"/api/v1/companies/{otherCompany}", Ct);
            await other.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");

            // La selección de empresa se ignora para quien no es Admin.
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/companies/{otherCompany}");
            request.Headers.Add("X-Company-Id", otherCompany.ToString());
            using var withHeader = await client.SendAsync(request, Ct);
            withHeader.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }
    }

    [Theory]
    [InlineData(UserRole.Analyst)]
    [InlineData(UserRole.Client)]
    public async Task NonAdmin_CannotModifyCompanies(UserRole role)
    {
        var (client, user) = await TestUsers.CreateAuthenticatedClientAsync(factory, role);
        using (client)
        {
            var body = new { name = "Cambio", industry = "Services" };
            await (await client.PostAsJsonAsync("/api/v1/companies", body, Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
            await (await client.PutAsJsonAsync($"/api/v1/companies/{user.CompanyId}", body, Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
            await (await client.DeleteAsync($"/api/v1/companies/{user.CompanyId}", Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
        }
    }

    [Fact]
    public async Task Admin_CanSelectCompanyWithHeaderOrQuery()
    {
        var companyA = await TestUsers.CreateCompanyAsync(factory.Database);
        var companyB = await TestUsers.CreateCompanyAsync(factory.Database);
        var (admin, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        using (admin)
        {
            var all = await (await admin.GetAsync("/api/v1/companies?pageSize=500", Ct)).ReadAsync<List<CompanyDto>>();
            all.Select(c => c.Id).ShouldContain(companyA);
            all.Select(c => c.Id).ShouldContain(companyB);

            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/companies");
            request.Headers.Add("X-Company-Id", companyA.ToString());
            var byHeader = await (await admin.SendAsync(request, Ct)).ReadAsync<List<CompanyDto>>();
            byHeader.ShouldHaveSingleItem().Id.ShouldBe(companyA);

            var byQuery = await (await admin.GetAsync($"/api/v1/companies?companyId={companyB}", Ct)).ReadAsync<List<CompanyDto>>();
            byQuery.ShouldHaveSingleItem().Id.ShouldBe(companyB);

            using var invalid = await admin.GetAsync("/api/v1/companies?companyId=abc", Ct);
            await invalid.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "tenant.invalid_company_id");
        }
    }
}
