using System.Net;
using Opsynex.Application.Companies;
using Opsynex.Domain.Enums;
using Opsynex.IntegrationTests.Infrastructure;

namespace Opsynex.IntegrationTests.Api;

/// <summary>Paginación de los listados que devuelven un arreglo (etapa 13).</summary>
public sealed class PagingTests(OpsynexApiFactory factory) : IClassFixture<OpsynexApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Lists_ArePaged_WithTotalCountHeader()
    {
        await TestUsers.CreateCompanyAsync(factory.Database);
        await TestUsers.CreateCompanyAsync(factory.Database);
        var (admin, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        using (admin)
        {
            using var all = await admin.GetAsync("/api/v1/companies?pageSize=500", Ct);
            var total = (await all.ReadAsync<List<CompanyDto>>()).Count;
            total.ShouldBeGreaterThanOrEqualTo(2);

            using var first = await admin.GetAsync("/api/v1/companies?page=1&pageSize=1", Ct);
            (await first.ReadAsync<List<CompanyDto>>()).Count.ShouldBe(1);
            first.Headers.GetValues("X-Total-Count").Single().ShouldBe(total.ToString(System.Globalization.CultureInfo.InvariantCulture));
            first.Headers.GetValues("X-Page-Size").Single().ShouldBe("1");

            using var beyond = await admin.GetAsync($"/api/v1/companies?page={total + 1}&pageSize=1", Ct);
            (await beyond.ReadAsync<List<CompanyDto>>()).ShouldBeEmpty();

            await (await admin.GetAsync("/api/v1/users?pageSize=501", Ct)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "paging.invalid_page_size");
            await (await admin.GetAsync("/api/v1/users?page=0", Ct)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "paging.invalid_page");
        }
    }
}
