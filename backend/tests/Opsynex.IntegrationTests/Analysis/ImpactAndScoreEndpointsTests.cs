using System.Net;
using Opsynex.Application.Analysis;
using Opsynex.Domain.Engines.Kpis;
using Opsynex.Domain.Enums;
using Opsynex.IntegrationTests.Infrastructure;
using Opsynex.IntegrationTests.Uploads;

namespace Opsynex.IntegrationTests.Analysis;

/// <summary>Punta a punta: subir → homologar → consolidar → KPIs, problemas, impacto y score coherentes.</summary>
public sealed class ImpactAndScoreEndpointsTests(OpsynexApiFactory factory) : IClassFixture<OpsynexApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<(HttpClient Analyst, HttpClient Client, Guid CompanyId)> CompanyWithBookAsync(string file)
    {
        var companyId = await TestUsers.CreateCompanyAsync(factory.Database);
        var (analyst, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst, companyId);
        var (client, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Client, companyId);
        (await analyst.UploadSampleAsync(file)).StatusCode.ShouldBe(HttpStatusCode.Created);
        return (analyst, client, companyId);
    }

    [Theory]
    [InlineData("libro_sintetico_3a.xls", new[] { 2022, 2023, 2024 })]
    [InlineData("libro_sintetico_2a.xls", new[] { 2023, 2024 })]
    public async Task UploadToDashboard_ReturnsCoherentKpisProblemsImpactAndScore(string file, int[] years)
    {
        var (analyst, client, _) = await CompanyWithBookAsync(file);
        using (analyst)
        using (client)
        {
            var kpis = await (await client.GetAsync("/api/v1/kpis", Ct)).ReadAsync<KpisResponse>();
            var problems = await (await client.GetAsync("/api/v1/problems?pageSize=100", Ct)).ReadAsync<ProblemPage>();
            var score = await (await client.GetAsync("/api/v1/score", Ct)).ReadAsync<ScoreResponse>();
            var latest = years.Max();
            var impact = await (await client.GetAsync("/api/v1/impact/summary", Ct)).ReadAsync<ImpactSummaryDto>();

            kpis.Years.Select(y => y.Year).ShouldBe(years);

            // Cada problema tiene un impacto mayor que 0 (los libros sintéticos no tienen alertas financieras).
            problems.Items.ShouldNotBeEmpty();
            problems.Items.ShouldAllBe(p => p.ImpactValue > 0m);
            problems.Items.ShouldAllBe(p => p.Recommendation.Contains("Impacto estimado", StringComparison.Ordinal));

            // Ranking: severidad y, dentro de cada severidad, impacto descendente.
            foreach (var group in problems.Items.GroupBy(p => p.Severity))
            {
                group.Select(p => p.ImpactValue!.Value).ShouldBeInOrder(SortDirection.Descending);
            }

            // Score entre 0 y 100 en todos los años, con interpretación coherente.
            score.Years.Select(y => y.Year).ShouldBe(years);
            foreach (var year in score.Years)
            {
                year.TotalScore.ShouldBeInRange(0m, 100m);
                year.InterpretationLabel.ShouldNotBeNullOrEmpty();
                year.Components.Count.ShouldBe(4);
                year.WeightsUsed.Values.Sum().ShouldBe(1m, 0.001m);
                year.Components.Where(c => c.Score is not null).ShouldAllBe(c => c.Score >= 0m && c.Score <= 100m);
            }

            // La nómina solo trae empleados del último año: antes de él, la productividad falta y su peso se redistribuye.
            var last = score.Years.Single(y => y.Year == latest);
            last.Components.Single(c => c.Dimension == KpiDimension.Productivity).Score.ShouldNotBeNull();
            last.WeightsRedistributed.ShouldBeFalse();
            last.WeightsUsed["Productivity"].ShouldBe(0.25m);
            last.ChangeFromPrevious.ShouldNotBeNull();
            var first = score.Years[0];
            first.WeightsRedistributed.ShouldBeTrue();
            first.WeightsUsed["Productivity"].ShouldBe(0m);
            first.ChangeFromPrevious.ShouldBeNull();

            // Rentabilidad = promedio de los KPIs normalizados del año.
            var normalized = kpis.Years.Single(y => y.Year == latest).Normalized;
            last.Components.Single(c => c.Dimension == KpiDimension.Profitability).Score
                .ShouldBe(decimal.Round((normalized["EbitdaMargin"]!.Value + normalized["GrossMargin"]!.Value) / 2m, 2));

            // Resumen de impacto del último año: total = suma de sus problemas = suma por categoría.
            var latestProblems = problems.Items.Where(p => p.Year == latest).ToList();
            impact.Year.ShouldBe(latest);
            impact.Currency.ShouldBe(kpis.Currency);
            impact.TotalEstimatedLoss.ShouldBe(latestProblems.Sum(p => p.ImpactValue!.Value));
            impact.ByCategory.Sum(c => c.EstimatedLoss).ShouldBe(impact.TotalEstimatedLoss);
            impact.ByCategory.Sum(c => c.Problems).ShouldBe(latestProblems.Count);
            impact.QuantifiedProblems.ShouldBe(latestProblems.Count);
            impact.UnquantifiedProblems.ShouldBe(0);
            impact.Top.Count.ShouldBe(Math.Min(5, latestProblems.Count));
            impact.Top.Select(t => t.ImpactValue).ShouldBeInOrder(SortDirection.Descending);
            impact.Top[0].ImpactValue.ShouldBe(latestProblems.Max(p => p.ImpactValue!.Value));
            impact.Revenue.ShouldNotBeNull();
            impact.LossPercentOfRevenue.ShouldBe(decimal.Round(impact.TotalEstimatedLoss / impact.Revenue!.Value * 100m, 2));

            // El análisis bajo demanda devuelve el mismo score y la misma pérdida.
            var run = await (await analyst.PostAsync("/api/v1/analysis/run", null, Ct)).ReadAsync<AnalysisRunResult>();
            run.LatestYear.ShouldBe(latest);
            run.LatestScore.ShouldBe(last.TotalScore);
            run.LatestEstimatedLoss.ShouldBe(impact.TotalEstimatedLoss);
        }
    }

    [Fact]
    public async Task MarginLossImpact_MatchesKpisAndRevenue()
    {
        var (analyst, client, _) = await CompanyWithBookAsync("libro_sintetico_3a.xls");
        using (analyst)
        using (client)
        {
            var kpis = await (await client.GetAsync("/api/v1/kpis", Ct)).ReadAsync<KpisResponse>();
            var page = await (await client.GetAsync("/api/v1/problems?year=2023", Ct)).ReadAsync<ProblemPage>();
            var item = page.Items.Single(p => p.Code == "EBITDA_DECLINE");
            var detail = await (await client.GetAsync($"/api/v1/problems/{item.Id}", Ct)).ReadAsync<ProblemDetailDto>();

            var impact = detail.Evidence["impact"]!;
            var revenue = impact["inputs"]!["revenue"]!.GetValue<decimal>();
            var m2022 = kpis.Years.Single(y => y.Year == 2022).Values[KpiCode.EbitdaMargin]!.Value;
            var m2023 = kpis.Years.Single(y => y.Year == 2023).Values[KpiCode.EbitdaMargin]!.Value;

            detail.ImpactValue.ShouldBe(decimal.Round((m2022 - m2023) * revenue, 2));
            impact["severityLevel"]!.GetValue<string>().ShouldBe(nameof(Severity.Medium));
            impact["formula"]!.GetValue<string>().ShouldContain("margen anterior");

            var summary2023 = await (await client.GetAsync("/api/v1/impact/summary?year=2023", Ct)).ReadAsync<ImpactSummaryDto>();
            summary2023.Year.ShouldBe(2023);
            summary2023.Top.ShouldContain(t => t.Id == item.Id && t.AffectedMarginPp == 3.05m);
            summary2023.ByCategory.ShouldContain(c => c.Category == ImpactCategory.MarginLoss);
        }
    }

    [Fact]
    public async Task Score_ForOneYear_AndEmptyCompany()
    {
        var (analyst, client, companyId) = await CompanyWithBookAsync("libro_sintetico_3a.xls");
        var (admin, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        var emptyCompany = await TestUsers.CreateCompanyAsync(factory.Database);
        using (analyst)
        using (client)
        using (admin)
        {
            var one = await (await client.GetAsync("/api/v1/score?year=2023", Ct)).ReadAsync<ScoreResponse>();
            one.Years.Select(y => y.Year).ShouldBe([2023]);
            (await (await client.GetAsync("/api/v1/score?year=2019", Ct)).ReadAsync<ScoreResponse>()).Years.ShouldBeEmpty();
            await (await client.GetAsync("/api/v1/score?year=1800", Ct)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "score.invalid_year");
            await (await client.GetAsync("/api/v1/impact/summary?year=3000", Ct)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "impact.invalid_year");

            // Admin: debe elegir la empresa; con una empresa sin datos todo viene vacío.
            await (await admin.GetAsync("/api/v1/score", Ct)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "score.company_required");
            await (await admin.GetAsync("/api/v1/impact/summary", Ct)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "impact.company_required");
            (await (await admin.GetAsync($"/api/v1/score?companyId={companyId}", Ct)).ReadAsync<ScoreResponse>()).Years.Count.ShouldBe(3);
            (await (await admin.GetAsync($"/api/v1/score?companyId={emptyCompany}", Ct)).ReadAsync<ScoreResponse>()).Years.ShouldBeEmpty();
            var emptyImpact = await (await admin.GetAsync($"/api/v1/impact/summary?companyId={emptyCompany}", Ct)).ReadAsync<ImpactSummaryDto>();
            emptyImpact.Year.ShouldBeNull();
            emptyImpact.TotalEstimatedLoss.ShouldBe(0m);
            emptyImpact.Top.ShouldBeEmpty();

            using var anonymous = factory.CreateClient();
            (await anonymous.GetAsync("/api/v1/score", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            (await anonymous.GetAsync("/api/v1/impact/summary", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }

    [Fact]
    public async Task RunningAnalysisTwice_KeepsOneScorePerYear()
    {
        var (analyst, client, companyId) = await CompanyWithBookAsync("libro_sintetico_3a.xls");
        using (analyst)
        using (client)
        {
            var before = await (await client.GetAsync("/api/v1/score", Ct)).ReadAsync<ScoreResponse>();
            (await analyst.PostAsync("/api/v1/analysis/run", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
            var after = await (await client.GetAsync("/api/v1/score", Ct)).ReadAsync<ScoreResponse>();

            after.Years.Select(y => (y.Year, y.TotalScore)).ShouldBe(before.Years.Select(y => (y.Year, y.TotalScore)));
            await using var context = factory.Database.CreateContext();
            context.Scores.Count(s => s.CompanyId == companyId).ShouldBe(3);
        }
    }
}
