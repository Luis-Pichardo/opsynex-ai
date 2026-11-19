using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Opsynex.Application.Analysis;
using Opsynex.Domain.Engines.Kpis;
using Opsynex.Domain.Enums;
using Opsynex.IntegrationTests.Infrastructure;
using Opsynex.IntegrationTests.Uploads;

namespace Opsynex.IntegrationTests.Analysis;

public sealed class ProblemsEndpointsTests(OpsynexApiFactory factory) : IClassFixture<OpsynexApiFactory>
{
    private const string ThreeYears = "libro_sintetico_3a.xls";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<(HttpClient Analyst, HttpClient Client, Guid CompanyId)> CompanyWithBookAsync(string file = ThreeYears)
    {
        var companyId = await TestUsers.CreateCompanyAsync(factory.Database);
        var (analyst, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst, companyId);
        var (client, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Client, companyId);
        (await analyst.UploadSampleAsync(file)).StatusCode.ShouldBe(HttpStatusCode.Created);
        return (analyst, client, companyId);
    }

    [Fact]
    public async Task ThreeYearBook_DetectsTheDesignedProblems()
    {
        var (analyst, client, _) = await CompanyWithBookAsync();
        using (analyst)
        using (client)
        {
            var page = await (await client.GetAsync("/api/v1/problems?pageSize=100", Ct)).ReadAsync<ProblemPage>();
            var codes = page.Items.Select(p => p.Code).Distinct().ToList();

            codes.Count.ShouldBeGreaterThanOrEqualTo(3);
            codes.ShouldContain("EBITDA_DECLINE");
            codes.ShouldContain("OVERHEAD_OUTPACES_REVENUE");
            codes.ShouldContain("LABOR_COST_OUTPACES_REVENUE");
            codes.ShouldContain("GROSS_MARGIN_DECLINE");

            // 2023: el margen EBITDA cae 3,05 pp → Medium; 2024: cae 1,14 pp → Low.
            page.Items.Single(p => p.Code == "EBITDA_DECLINE" && p.Year == 2023).Severity.ShouldBe(Severity.Medium);
            page.Items.Single(p => p.Code == "EBITDA_DECLINE" && p.Year == 2024).Severity.ShouldBe(Severity.Low);
            // El overhead solo supera a los ingresos en 2024 (7,58 % frente a 7,41 %).
            page.Items.Where(p => p.Code == "OVERHEAD_OUTPACES_REVENUE").Select(p => p.Year).ShouldBe([2024]);
            // Ninguna alerta financiera: liquidez y endeudamiento están sanos.
            codes.ShouldNotContain("LOW_LIQUIDITY");
            codes.ShouldNotContain("HIGH_LEVERAGE");

            // Orden de prioridad: severidad primero; prioridad 1, 2, 3…
            page.Items.Select(p => p.Priority).ShouldBe(Enumerable.Range(1, page.Items.Count));
            page.Items.Select(p => (int)p.Severity).ShouldBeInOrder(SortDirection.Descending);
            page.Items.ShouldAllBe(p => p.Status == ProblemStatus.Open && p.Description.Length > 0 && p.Recommendation.Length > 0);

            // Hallazgos positivos del último año: menor endeudamiento y crecimiento de ingresos.
            page.Highlights.Select(h => h.Code).ShouldContain("DebtToEquity");
            page.Highlights.Select(h => h.Code).ShouldContain("RevenueGrowth");
        }
    }

    [Fact]
    public async Task RunningAnalysisTwice_DoesNotDuplicate_AndKeepsUserStatus()
    {
        var (analyst, client, companyId) = await CompanyWithBookAsync();
        using (analyst)
        using (client)
        {
            var before = await (await client.GetAsync("/api/v1/problems?pageSize=100", Ct)).ReadAsync<ProblemPage>();
            var target = before.Items.First(p => p.Code == "EBITDA_DECLINE");

            var patched = await (await analyst.PatchAsJsonAsync($"/api/v1/problems/{target.Id}",
                new UpdateProblemStatusRequest(ProblemStatus.Acknowledged), Json.Options, Ct)).ReadAsync<ProblemDetailDto>();
            patched.Status.ShouldBe(ProblemStatus.Acknowledged);

            for (var i = 0; i < 2; i++)
            {
                (await analyst.PostAsync("/api/v1/analysis/run", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
            }

            (await analyst.UploadSampleAsync(ThreeYears)).StatusCode.ShouldBe(HttpStatusCode.Created); // nueva carga: vuelve a analizar

            var after = await (await client.GetAsync("/api/v1/problems?pageSize=100", Ct)).ReadAsync<ProblemPage>();
            after.TotalCount.ShouldBe(before.TotalCount);
            after.Items.Single(p => p.Id == target.Id).Status.ShouldBe(ProblemStatus.Acknowledged);

            await using var context = factory.Database.CreateContext();
            var stored = await context.Problems.Where(p => p.CompanyId == companyId).ToListAsync(Ct);
            stored.Count.ShouldBe(before.TotalCount);
            stored.GroupBy(p => (p.Year, p.Code)).ShouldAllBe(g => g.Count() == 1);
        }
    }

    [Fact]
    public async Task List_FiltersAndPaginates()
    {
        var (analyst, client, _) = await CompanyWithBookAsync();
        using (analyst)
        using (client)
        {
            var all = await (await client.GetAsync("/api/v1/problems?pageSize=100", Ct)).ReadAsync<ProblemPage>();

            var year2024 = await (await client.GetAsync("/api/v1/problems?year=2024", Ct)).ReadAsync<ProblemPage>();
            year2024.Items.ShouldAllBe(p => p.Year == 2024);

            var costs = await (await client.GetAsync("/api/v1/problems?category=CostEfficiency", Ct)).ReadAsync<ProblemPage>();
            costs.Items.ShouldNotBeEmpty();
            costs.Items.ShouldAllBe(p => p.Category == ProblemCategory.CostEfficiency);

            var medium = await (await client.GetAsync("/api/v1/problems?severity=Medium", Ct)).ReadAsync<ProblemPage>();
            medium.Items.ShouldAllBe(p => p.Severity == Severity.Medium);

            (await (await client.GetAsync("/api/v1/problems?status=Resolved", Ct)).ReadAsync<ProblemPage>()).TotalCount.ShouldBe(0);

            var page1 = await (await client.GetAsync("/api/v1/problems?page=1&pageSize=2", Ct)).ReadAsync<ProblemPage>();
            var page2 = await (await client.GetAsync("/api/v1/problems?page=2&pageSize=2", Ct)).ReadAsync<ProblemPage>();
            page1.Items.Count.ShouldBe(2);
            page1.TotalCount.ShouldBe(all.TotalCount);
            page2.Items[0].Priority.ShouldBe(3);
            page1.Items.Select(p => p.Id).Concat(page2.Items.Select(p => p.Id)).ShouldBe(all.Items.Take(4).Select(p => p.Id));

            await (await client.GetAsync("/api/v1/problems?pageSize=500", Ct)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "problems.invalid_page");
        }
    }

    [Fact]
    public async Task Detail_IncludesEvidenceAndKpiHistory()
    {
        var (analyst, client, _) = await CompanyWithBookAsync();
        using (analyst)
        using (client)
        {
            var page = await (await client.GetAsync("/api/v1/problems?year=2023", Ct)).ReadAsync<ProblemPage>();
            var item = page.Items.Single(p => p.Code == "EBITDA_DECLINE");

            var detail = await (await client.GetAsync($"/api/v1/problems/{item.Id}", Ct)).ReadAsync<ProblemDetailDto>();

            detail.RelatedKpi.ShouldBe(KpiCode.EbitdaMargin);
            detail.Evidence["changePp"]!.GetValue<decimal>().ShouldBe(-3.05m);
            detail.Evidence["previous"]!.GetValue<decimal>().ShouldBe(0.228m);
            detail.Description.ShouldContain("22,8 %");
            detail.KpiHistory.Select(h => h.Year).ShouldBe([2022, 2023, 2024]);
            detail.KpiHistory[2].Value.ShouldBe(0.1861m);
            // Impacto (etapa 07): (22,8 % − 19,75 %) × ingresos de 2023 = 3,05 pp de margen.
            detail.ImpactValue.ShouldNotBeNull().ShouldBeGreaterThan(0m);
            detail.Evidence["impact"]!["affectedMarginPp"]!.GetValue<decimal>().ShouldBe(3.05m);
            detail.Recommendation.ShouldContain("Impacto estimado: US$"); // moneda de la empresa de prueba
        }
    }

    [Fact]
    public async Task Permissions_AndCompanyIsolation()
    {
        var (analyst, client, _) = await CompanyWithBookAsync("libro_sintetico_2a.xls");
        var (other, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst);
        var (admin, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        using (analyst)
        using (client)
        using (other)
        using (admin)
        {
            var problem = (await (await client.GetAsync("/api/v1/problems", Ct)).ReadAsync<ProblemPage>()).Items[0];

            await (await client.PatchAsJsonAsync($"/api/v1/problems/{problem.Id}", new UpdateProblemStatusRequest(ProblemStatus.Resolved), Json.Options, Ct))
                .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
            await (await other.GetAsync($"/api/v1/problems/{problem.Id}", Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
            await (await other.PatchAsJsonAsync($"/api/v1/problems/{problem.Id}", new UpdateProblemStatusRequest(ProblemStatus.Resolved), Json.Options, Ct))
                .ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
            (await (await other.GetAsync("/api/v1/problems", Ct)).ReadAsync<ProblemPage>()).TotalCount.ShouldBe(0);
            await (await admin.GetAsync("/api/v1/problems", Ct)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "problems.company_required");
            (await admin.GetAsync($"/api/v1/problems/{problem.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

            using var invalid = new StringContent("""{"status":"Closed"}""", System.Text.Encoding.UTF8, "application/json");
            (await analyst.PatchAsync($"/api/v1/problems/{problem.Id}", invalid, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }
    }
}
