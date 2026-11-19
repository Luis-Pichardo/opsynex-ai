using System.Net;
using Opsynex.Application.Uploads;
using Opsynex.Domain.Enums;
using Opsynex.IntegrationTests.Infrastructure;

namespace Opsynex.IntegrationTests.Uploads;

public sealed class DataEndpointsTests(OpsynexApiFactory factory) : IClassFixture<OpsynexApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Data_ReturnsRawMonthlyAndOperationalData()
    {
        var (analyst, user) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst);
        var (viewer, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Client, user.CompanyId);
        using (analyst)
        using (viewer)
        {
            var upload = await (await analyst.UploadSampleAsync("libro_sintetico_2a.xls")).ReadAsync<UploadReportDto>(HttpStatusCode.Created);

            var raw = await (await viewer.GetAsync("/api/v1/data?year=2024&type=Raw&sheetType=IncomeStatement", Ct)).ReadAsync<DataResponse>();
            raw.UploadId.ShouldBe(upload.UploadId);
            raw.AvailableYears.ShouldBe([2023, 2024]);
            raw.Raw!.ShouldAllBe(l => l.SheetType == SheetType.IncomeStatement && l.Values.Keys.Single() == 2024);
            raw.Raw!.ShouldContain(l => l.NormalizedLabel == "Ingresos Netos");

            var all = await (await viewer.GetAsync("/api/v1/data", Ct)).ReadAsync<DataResponse>();
            all.Raw!.Select(l => l.SheetType).Distinct().ShouldContain(SheetType.TrialBalance);

            var monthly = await (await viewer.GetAsync("/api/v1/data?type=Monthly", Ct)).ReadAsync<DataResponse>();
            monthly.Monthly!.Count.ShouldBe(12);
            monthly.Monthly![0].Month.ShouldBe(1);

            var operational = await (await viewer.GetAsync("/api/v1/data?type=Operational&year=2024", Ct)).ReadAsync<DataResponse>();
            operational.Operational!.Single().EmployeeCount.ShouldBe(15);

            var financial = await (await viewer.GetAsync("/api/v1/data?type=Financial", Ct)).ReadAsync<DataResponse>();
            // Consolidado por la homologación automática (aquí con el fallback local, sin IA).
            financial.Financial!.Select(f => f.Year).ShouldBe([2023, 2024]);
            financial.Financial![1].Revenue.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task Admin_MustSelectCompany()
    {
        var companyId = await TestUsers.CreateCompanyAsync(factory.Database);
        var (admin, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        using (admin)
        {
            await (await admin.GetAsync("/api/v1/data", Ct)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "data.company_required");

            var empty = await (await admin.GetAsync($"/api/v1/data?companyId={companyId}", Ct)).ReadAsync<DataResponse>();
            empty.CompanyId.ShouldBe(companyId);
            empty.Raw!.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task Data_RequiresSession()
    {
        using var client = factory.CreateClient();

        await (await client.GetAsync("/api/v1/data", Ct)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }
}
