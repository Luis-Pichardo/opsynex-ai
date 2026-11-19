using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Opsynex.Application.Analysis;
using Opsynex.Domain.Engines.Kpis;
using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;
using Opsynex.IntegrationTests.Infrastructure;
using Opsynex.IntegrationTests.Uploads;

namespace Opsynex.IntegrationTests.Analysis;

/// <summary>KPIs del libro sintético contra los calculados a mano por el generador (expected_values.json → kpis).</summary>
public sealed class KpisEndpointsTests(OpsynexApiFactory factory) : IClassFixture<OpsynexApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly JsonDocument Manifest =
        JsonDocument.Parse(File.ReadAllText(Path.Combine(UploadClient.SampleDirectory, "expected_values.json")));

    private static decimal? ExpectedKpi(string file, int year, KpiCode code)
    {
        var value = Manifest.RootElement.GetProperty(file).GetProperty("kpis")
            .GetProperty(year.ToString(CultureInfo.InvariantCulture)).GetProperty(code.ToString());
        return value.ValueKind == JsonValueKind.Null ? null : value.GetDecimal();
    }

    /// <summary>Empresa en RD$ con un Analyst y un Client.</summary>
    private async Task<(HttpClient Analyst, HttpClient Client, Guid CompanyId)> CompanyInDopAsync()
    {
        Guid companyId;
        await using (var context = factory.Database.CreateContext())
        {
            var company = new Company { Name = $"Empresa {Guid.NewGuid():N}", Industry = Industry.Manufacturing, Currency = "DOP" };
            context.Companies.Add(company);
            await context.SaveChangesAsync(Ct);
            companyId = company.Id;
        }

        var (analyst, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst, companyId);
        var (client, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Client, companyId);
        return (analyst, client, companyId);
    }

    [Theory]
    [InlineData("libro_sintetico_2a.xls")]
    [InlineData("libro_sintetico_3a.xls")]
    public async Task Kpis_MatchHandCalculatedValues_ForEveryYear(string file)
    {
        var (analyst, client, companyId) = await CompanyInDopAsync();
        using (analyst)
        using (client)
        {
            (await analyst.UploadSampleAsync(file)).StatusCode.ShouldBe(HttpStatusCode.Created);

            var response = await (await client.GetAsync("/api/v1/kpis", Ct)).ReadAsync<KpisResponse>();

            response.CompanyId.ShouldBe(companyId);
            response.Currency.ShouldBe("DOP");
            response.Definitions.Count.ShouldBe(14);
            var years = Manifest.RootElement.GetProperty(file).GetProperty("years").EnumerateArray().Select(y => y.GetInt32()).ToList();
            response.Years.Select(y => y.Year).ShouldBe(years);

            var mismatches = new List<string>();
            foreach (var year in response.Years)
            {
                foreach (var code in Enum.GetValues<KpiCode>())
                {
                    var expected = ExpectedKpi(file, year.Year, code);
                    if (year.Values[code] != expected)
                    {
                        mismatches.Add($"{year.Year} {code}: {year.Values[code]} ≠ {expected}");
                    }
                }
            }

            mismatches.ShouldBeEmpty(string.Join("\n", mismatches));
        }
    }

    [Fact]
    public async Task Kpis_IncludeTrendsNormalizationAndMonthlySeries()
    {
        const string file = "libro_sintetico_2a.xls";
        var (analyst, client, _) = await CompanyInDopAsync();
        using (analyst)
        using (client)
        {
            (await analyst.UploadSampleAsync(file)).StatusCode.ShouldBe(HttpStatusCode.Created);

            var response = await (await client.GetAsync("/api/v1/kpis", Ct)).ReadAsync<KpisResponse>();
            var first = response.Years[0];
            var last = response.Years[1];

            first.Trends.ShouldBeNull(); // primer año: sin año anterior
            last.Trends.ShouldNotBeNull();
            last.Trends.PreviousYear.ShouldBe(2023);

            // El margen EBITDA baja de 19,75 % a 18,61 %: desfavorable.
            var ebitda = last.Trends.Kpis[KpiCode.EbitdaMargin];
            ebitda.Previous.ShouldBe(ExpectedKpi(file, 2023, KpiCode.EbitdaMargin));
            ebitda.Current.ShouldBe(ExpectedKpi(file, 2024, KpiCode.EbitdaMargin));
            ebitda.Direction.ShouldBe(TrendDirection.Down);
            ebitda.Favorable.ShouldBe(false);
            // La eficiencia de costos sube (más costo por cada peso de ingreso): desfavorable.
            last.Trends.Kpis[KpiCode.CostEfficiency].Direction.ShouldBe(TrendDirection.Up);
            last.Trends.Kpis[KpiCode.CostEfficiency].Favorable.ShouldBe(false);
            // Ingresos por empleado solo existe en 2024 (la nómina es de ese año): sin tendencia.
            last.Trends.Kpis[KpiCode.RevenuePerEmployee].Direction.ShouldBeNull();
            // Crecimiento de ingresos, costo total y gastos generales (para el diagnóstico).
            last.Trends.RevenueGrowth!.ChangePercent.ShouldBe(7.41m);
            last.Trends.TotalCostGrowth!.Direction.ShouldBe(TrendDirection.Up);
            last.Trends.OverheadGrowth!.Current.ShouldBe(8_520_000m);

            // Normalización (manufactura en RD$): margen EBITDA 0 % → 0 pts, 20 % → 100 pts.
            last.Normalized["EbitdaMargin"].ShouldBe(93.05m);
            last.Normalized["RevenuePerEmployee"].ShouldBe(78.64m); // (12.116.066,67 − 1,5 M) / 13,5 M
            last.Normalized["CostPerUnit"].ShouldBeNull();          // sin benchmark (depende del producto)
            last.Normalized["CurrentRatio"].ShouldBe(100m);         // 2,77 > 2,0: tope de 100

            // Serie mensual (hoja CUADRO).
            response.Monthly.ShouldNotBeNull();
            response.Monthly.Count.ShouldBe(12);
            response.Monthly[0].GrowthPercent.ShouldBeNull();
            response.Monthly[1].GrowthPercent.ShouldNotBeNull();
            response.Monthly[2].MovingAverage3.ShouldNotBeNull();
            response.Monthly.Average(m => m.SeasonalityIndex!.Value).ShouldBe(1m, 0.0001m);
        }
    }

    [Fact]
    public async Task Kpis_FilteredByYear()
    {
        var (analyst, client, _) = await CompanyInDopAsync();
        using (analyst)
        using (client)
        {
            (await analyst.UploadSampleAsync("libro_sintetico_2a.xls")).StatusCode.ShouldBe(HttpStatusCode.Created);

            var response = await (await client.GetAsync("/api/v1/kpis?year=2023", Ct)).ReadAsync<KpisResponse>();

            response.Years.ShouldHaveSingleItem().Year.ShouldBe(2023);
            response.Monthly.ShouldBeNull(); // la serie mensual es de 2024
            (await (await client.GetAsync("/api/v1/kpis?year=2019", Ct)).ReadAsync<KpisResponse>()).Years.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task KpiResults_AreStoredWithTrendsAndNormalizedJson()
    {
        var (analyst, client, companyId) = await CompanyInDopAsync();
        using (analyst)
        using (client)
        {
            (await analyst.UploadSampleAsync("libro_sintetico_2a.xls")).StatusCode.ShouldBe(HttpStatusCode.Created);

            await using var context = factory.Database.CreateContext();
            var stored = await context.KpiResults.Where(k => k.CompanyId == companyId).OrderBy(k => k.Year).ToListAsync(Ct);
            stored.Select(k => k.Year).ShouldBe([2023, 2024]);
            stored[1].EbitdaMargin.ShouldBe(0.1861m);
            stored[1].Trends.Kpis[KpiCode.EbitdaMargin].Direction.ShouldBe(TrendDirection.Down);
            stored[1].Normalized.ShouldContainKey("GrossMargin");
        }
    }

    [Fact]
    public async Task AnalysisRun_RecalculatesKpisOnDemand()
    {
        var (analyst, client, companyId) = await CompanyInDopAsync();
        using (analyst)
        using (client)
        {
            (await analyst.UploadSampleAsync("libro_sintetico_2a.xls")).StatusCode.ShouldBe(HttpStatusCode.Created);

            // Se cambian los empleados de 2024 y se recalcula bajo demanda.
            await using (var context = factory.Database.CreateContext())
            {
                var ops = await context.OperationalData.SingleAsync(o => o.CompanyId == companyId && o.Year == 2024, Ct);
                ops.EmployeeCount = 30;
                ops.UnitsProduced = 1_000_000;
                await context.SaveChangesAsync(Ct);
            }

            var run = await (await analyst.PostAsync("/api/v1/analysis/run", null, Ct)).ReadAsync<AnalysisRunResult>();
            run.Years.ShouldBe([2023, 2024]);

            var response = await (await client.GetAsync("/api/v1/kpis?year=2024", Ct)).ReadAsync<KpisResponse>();
            var values = response.Years.Single().Values;
            var revenue = Manifest.RootElement.GetProperty("libro_sintetico_2a.xls").GetProperty("values").GetProperty("2024").GetProperty("net_sales").GetDecimal();
            values[KpiCode.RevenuePerEmployee].ShouldBe(decimal.Round(revenue / 30, 2));
            values[KpiCode.CostPerUnit].ShouldNotBeNull();

            using var forbidden = await client.PostAsync("/api/v1/analysis/run", null, Ct);
            await forbidden.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
        }
    }

    [Fact]
    public async Task Admin_MustSelectCompany()
    {
        var (admin, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        using (admin)
        {
            await (await admin.GetAsync("/api/v1/kpis", Ct)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "kpis.company_required");
            await (await admin.PostAsync("/api/v1/analysis/run", null, Ct)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "analysis.company_required");

            var companyId = await TestUsers.CreateCompanyAsync(factory.Database);
            var empty = await (await admin.GetAsync($"/api/v1/kpis?companyId={companyId}", Ct)).ReadAsync<KpisResponse>();
            empty.Years.ShouldBeEmpty();
            empty.Monthly.ShouldBeNull();
        }
    }
}
