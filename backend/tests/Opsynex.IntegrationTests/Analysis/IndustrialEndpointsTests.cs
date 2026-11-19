using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Opsynex.Application.Analysis;
using Opsynex.Application.Industrial;
using Opsynex.Application.Uploads;
using Opsynex.Domain.Engines.Industrial;
using Opsynex.Domain.Enums;
using Opsynex.IntegrationTests.Infrastructure;
using Opsynex.IntegrationTests.Uploads;

namespace Opsynex.IntegrationTests.Analysis;

/// <summary>
/// Módulo industrial punta a punta con <c>sample-data/industrial/</c> (3 máquinas, 2 líneas, 3 meses):
/// carga → KPIs → reglas con impacto → endpoints. Los valores esperados vienen de
/// <c>expected_industrial.json</c>, calculado por el generador de forma independiente al motor .NET.
/// </summary>
public sealed class IndustrialEndpointsTests(OpsynexApiFactory factory) : IClassFixture<OpsynexApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly JsonElement Expected = JsonDocument.Parse(
        File.ReadAllText(Path.Combine(UploadClient.SampleDirectory, "industrial", "expected_industrial.json"))).RootElement;

    private static decimal Machine(string id, string field) => Expected.GetProperty("machines").GetProperty(id).GetProperty(field).GetDecimal();

    private async Task<(HttpClient Analyst, HttpClient Client, Guid CompanyId)> CompanyAsync()
    {
        var companyId = await TestUsers.CreateCompanyAsync(factory.Database);
        await using (var context = factory.Database.CreateContext())
        {
            var company = await context.Companies.SingleAsync(c => c.Id == companyId, Ct);
            company.Currency = "DOP";
            await context.SaveChangesAsync(Ct);
        }

        var (analyst, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst, companyId);
        var (client, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Client, companyId);
        return (analyst, client, companyId);
    }

    private static async Task<UploadReportDto> UploadAsync(HttpClient analyst, string file) =>
        await (await analyst.UploadSampleAsync(Path.Combine("industrial", file))).ReadAsync<UploadReportDto>(HttpStatusCode.Created);

    private async Task<(HttpClient Analyst, HttpClient Client, Guid CompanyId)> CompanyWithIndustrialDataAsync()
    {
        var company = await CompanyAsync();
        await UploadAsync(company.Analyst, "estandares.xlsx");
        await UploadAsync(company.Analyst, "costos.xlsx");
        await UploadAsync(company.Analyst, "produccion.xlsx");
        return company;
    }

    [Fact]
    public async Task Uploads_AreDetectedByTheirHeaders_AndStoredAsAggregates()
    {
        var (analyst, client, _) = await CompanyAsync();
        using (analyst)
        using (client)
        {
            var standards = await UploadAsync(analyst, "estandares.xlsx");
            standards.FileType.ShouldBe(UploadFileType.Standards);
            standards.Status.ShouldBe(UploadStatus.Processed);
            standards.RowsCount.ShouldBe(3);

            var production = await UploadAsync(analyst, "produccion.xlsx");
            production.FileType.ShouldBe(UploadFileType.Production);
            production.Status.ShouldBe(UploadStatus.Processed);
            production.AnalysisStatus.ShouldBe(AnalysisStatus.Completed);
            production.Errors.ShouldBeEmpty();
            production.Years.ShouldBe([2024]);
            production.Sheets.Single().DetectedType.ShouldBe(SheetType.Production);
            production.Summary.IndustrialRecords.ShouldBe(Expected.GetProperty("period").GetProperty("days").GetInt32() * 3);
            production.Summary.Machines.ShouldBe(3);
            production.Summary.PeriodFrom.ShouldBe(new DateOnly(2024, 10, 1));
            production.Summary.PeriodTo.ShouldBe(new DateOnly(2024, 12, 31));

            // Sin costos todavía: se avisa que falta el costo por unidad.
            production.Warnings.ShouldContain(w => w.Code == "industrial.incomplete_data" && w.Message.Contains("Sin costos", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task SampleData_DetectsTheThreeSpecProblems_EachWithItsImpact()
    {
        var (analyst, client, _) = await CompanyWithIndustrialDataAsync();
        using (analyst)
        using (client)
        {
            var problems = (await (await client.GetAsync("/api/v1/problems?year=2024&pageSize=100", Ct)).ReadAsync<ProblemPage>()).Items;
            var expected = Expected.GetProperty("problems");

            foreach (var (code, severity) in new[]
            {
                (IndustrialDiagnosisEngine.LowOee, Severity.High),
                (IndustrialDiagnosisEngine.HighScrap, Severity.Medium),
                (IndustrialDiagnosisEngine.HighDowntime, Severity.High),
                (IndustrialDiagnosisEngine.CostAboveStandard, Severity.Medium),
            })
            {
                var problem = problems.Single(p => p.Code == code);
                problem.Severity.ShouldBe(severity);
                problem.ImpactValue.ShouldNotBeNull();
                var impact = expected.GetProperty(code).GetProperty("impact").GetDecimal();
                problem.ImpactValue!.Value.ShouldBe(impact, impact * 0.001m, code);
                problem.ImpactValue.Value.ShouldBeGreaterThan(0m);
                problem.Recommendation.ShouldContain("Impacto estimado: RD$");
                problem.Recommendation.ShouldContain("oct–dic 2024");
                foreach (var machine in expected.GetProperty(code).GetProperty("machines").EnumerateArray())
                {
                    problem.Description.ShouldContain(machine.GetString()!);
                }
            }

            // MAQ-01 está sana: no aparece en ningún problema industrial.
            problems.Where(p => IndustrialDiagnosisEngine.Codes.Contains(p.Code)).ShouldAllBe(p => !p.Description.Contains("MAQ-01"));

            // El detalle muestra el cálculo del impacto.
            var downtime = problems.Single(p => p.Code == IndustrialDiagnosisEngine.HighDowntime);
            var detail = await (await client.GetAsync($"/api/v1/problems/{downtime.Id}", Ct)).ReadAsync<JsonElement>();
            var evidence = detail.GetProperty("evidence");
            evidence.GetProperty("kind").GetString().ShouldBe("industrial");
            evidence.GetProperty("impact").GetProperty("formula").GetString().ShouldBe("minutos de parada × costo por minuto");
            evidence.GetProperty("impact").GetProperty("inputs").GetProperty("machines").GetProperty("MAQ-03")
                .GetProperty("costPerMinute").GetDecimal().ShouldBe(42m);
        }
    }

    [Fact]
    public async Task Kpis_MatchTheIndependentCalculation_ByMachineLineAndPlant()
    {
        var (analyst, client, _) = await CompanyWithIndustrialDataAsync();
        using (analyst)
        using (client)
        {
            var kpis = await (await client.GetAsync("/api/v1/industrial/kpis", Ct)).ReadAsync<IndustrialKpisDto>();

            kpis.HasData.ShouldBeTrue();
            kpis.Currency.ShouldBe("DOP");
            kpis.Years.ShouldBe([2024]);
            kpis.Year.ShouldBe(2024);
            kpis.Month.ShouldBeNull();
            kpis.Months.ShouldBe([10, 11, 12]);
            kpis.Lines.ShouldBe(["LINEA-1", "LINEA-2"]);
            kpis.Machines.Select(m => (m.MachineId, m.LineId, m.HasStandard, m.HasCosts))
                .ShouldBe([("MAQ-01", "LINEA-1", true, true), ("MAQ-02", "LINEA-1", true, true), ("MAQ-03", "LINEA-2", true, true)]);
            kpis.Warnings.ShouldBeEmpty();
            kpis.Thresholds.LowOee.ShouldBe(0.60m);

            foreach (var row in kpis.ByMachine)
            {
                var id = row.MachineId!;
                row.Level.ShouldBe(IndustrialLevel.Machine);
                row.DowntimeMinutes.ShouldBe(Machine(id, "downtime"));
                row.TotalUnits.ShouldBe(Machine(id, "total"));
                row.GoodUnits.ShouldBe(Machine(id, "good"));
                row.Kpis.Availability!.Value.ShouldBe(Machine(id, "availability"), 0.0001m);
                row.Kpis.Performance!.Value.ShouldBe(Machine(id, "performance"), 0.0001m);
                row.Kpis.Quality!.Value.ShouldBe(Machine(id, "quality"), 0.0001m);
                row.Kpis.Oee!.Value.ShouldBe(Machine(id, "oee"), 0.0002m);
                row.Kpis.ScrapRate!.Value.ShouldBe(Machine(id, "scrapRate"), 0.0001m);
                row.Kpis.DowntimeRatio!.Value.ShouldBe(Machine(id, "downtimeRatio"), 0.0001m);
                row.Kpis.CapacityUtilization!.Value.ShouldBe(Machine(id, "capacityUtilization"), 0.0001m);
                row.Kpis.CostPerUnit!.Value.ShouldBe(Machine(id, "costPerUnit"), 0.01m);
                row.Kpis.StandardCostPerUnit!.Value.ShouldBe(Machine(id, "standardCost"));
                row.Losses.DowntimeCost!.Value.ShouldBe(Machine(id, "downtimeCost"), 0.01m);
                row.Losses.ScrapCost!.Value.ShouldBe(Machine(id, "scrapCost"), 0.01m);
                row.Losses.Total.ShouldBe(row.Losses.DowntimeCost + row.Losses.ScrapCost);
                row.Costs!.Total.ShouldBe(row.Costs.Labor + row.Costs.Material + row.Costs.Energy + row.Costs.Overhead);
            }

            // La planta se calcula con las sumas (no promediando ratios).
            var summary = kpis.Summary!;
            summary.Level.ShouldBe(IndustrialLevel.Total);
            summary.DowntimeMinutes.ShouldBe(kpis.ByMachine.Sum(m => m.DowntimeMinutes));
            summary.Kpis.Availability!.Value.ShouldBe(decimal.Round(1m - (summary.DowntimeMinutes / summary.PlannedMinutes), 4));
            summary.Losses.Total.ShouldBe(kpis.ByMachine.Sum(m => m.Losses.Total));
            kpis.ByLine.Select(l => l.LineId).ShouldBe(["LINEA-1", "LINEA-2"]);
            kpis.ByLine[1].Kpis.Oee.ShouldBe(kpis.ByMachine.Single(m => m.MachineId == "MAQ-03").Kpis.Oee);
            kpis.Monthly.Select(m => m.Period).ShouldBe(["2024-10", "2024-11", "2024-12"]);
            kpis.Monthly.Sum(m => m.TotalUnits).ShouldBe(summary.TotalUnits);
            kpis.Daily.Count.ShouldBe(Expected.GetProperty("period").GetProperty("days").GetInt32());

            // El día de la avería se ve en la serie diaria.
            var outage = kpis.Daily.Single(d => d.Date == Expected.GetProperty("outageDay").GetString());
            outage.DowntimeMinutes.ShouldBe(kpis.Daily.Max(d => d.DowntimeMinutes));
            outage.Oee.ShouldBe(kpis.Daily.Min(d => d.Oee));
        }
    }

    [Fact]
    public async Task Kpis_FilterByMonthMachineAndLine()
    {
        var (analyst, client, _) = await CompanyWithIndustrialDataAsync();
        using (analyst)
        using (client)
        {
            var machine = await (await client.GetAsync("/api/v1/industrial/kpis?year=2024&month=11&machineId=maq-03", Ct)).ReadAsync<IndustrialKpisDto>();
            machine.MachineId.ShouldBe("MAQ-03");
            machine.Month.ShouldBe(11);
            machine.Summary!.Level.ShouldBe(IndustrialLevel.Machine);
            machine.Summary.Period.ShouldBe("2024-11");
            machine.ByMachine.Select(m => m.MachineId).ShouldBe(["MAQ-03"]);
            machine.Monthly.Select(m => m.Period).ShouldBe(["2024-10", "2024-11", "2024-12"]);
            machine.Monthly.ShouldAllBe(m => m.MachineId == "MAQ-03");
            machine.Daily.ShouldAllBe(d => d.Date.StartsWith("2024-11", StringComparison.Ordinal));

            var line = await (await client.GetAsync("/api/v1/industrial/kpis?lineId=LINEA-1", Ct)).ReadAsync<IndustrialKpisDto>();
            line.Summary!.Level.ShouldBe(IndustrialLevel.Line);
            line.ByMachine.Select(m => m.MachineId).ShouldBe(["MAQ-01", "MAQ-02"]);
            line.Summary.TotalUnits.ShouldBe(line.ByMachine.Sum(m => m.TotalUnits));

            await (await client.GetAsync("/api/v1/industrial/kpis?month=13", Ct)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "industrial.invalid_month");
            await (await client.GetAsync("/api/v1/industrial/kpis?year=2023", Ct)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "industrial.no_data_for_year");
            await (await client.GetAsync("/api/v1/industrial/kpis?machineId=MAQ-99", Ct)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "industrial.unknown_machine");
            await (await client.GetAsync("/api/v1/industrial/score?lineId=LINEA-9", Ct)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "industrial.unknown_line");
        }
    }

    [Fact]
    public async Task Score_CombinesProductionCostFinancialAndStandard()
    {
        var (analyst, client, _) = await CompanyWithIndustrialDataAsync();
        using (analyst)
        using (client)
        {
            // Sin estados financieros: el peso financiero (0,25) se reparte entre los otros tres.
            var withoutFinancial = await (await client.GetAsync("/api/v1/industrial/score", Ct)).ReadAsync<IndustrialScoreDto>();
            withoutFinancial.HasData.ShouldBeTrue();
            withoutFinancial.TotalScore.ShouldNotBeNull();
            withoutFinancial.TotalScore!.Value.ShouldBeInRange(0m, 100m);
            withoutFinancial.WeightsRedistributed.ShouldBeTrue();
            withoutFinancial.Components.Select(c => c.Dimension)
                .ShouldBe([IndustrialDimension.Production, IndustrialDimension.Cost, IndustrialDimension.Financial, IndustrialDimension.Standard]);
            var financial = withoutFinancial.Components.Single(c => c.Dimension == IndustrialDimension.Financial);
            financial.Score.ShouldBeNull();
            financial.Weight.ShouldBe(0m);
            financial.MissingReason.ShouldNotBeNull();
            withoutFinancial.Components.Sum(c => c.Weight).ShouldBe(1m, 0.001m);

            // Con el libro contable de 2023–2024: el Score Opsynex de 2024 entra como componente financiero.
            (await analyst.UploadSampleAsync("libro_sintetico_2a.xls")).StatusCode.ShouldBe(HttpStatusCode.Created);
            var opsynex = (await (await client.GetAsync("/api/v1/score?year=2024", Ct)).ReadAsync<ScoreResponse>()).Years.Single();
            var score = await (await client.GetAsync("/api/v1/industrial/score?year=2024", Ct)).ReadAsync<IndustrialScoreDto>();
            score.OpsynexScore.ShouldBe(opsynex.TotalScore);
            score.WeightsRedistributed.ShouldBeFalse();
            score.Components.Select(c => c.BaseWeight).ShouldBe([0.30m, 0.25m, 0.25m, 0.20m]);
            score.Components.Single(c => c.Dimension == IndustrialDimension.Financial).Score.ShouldBe(opsynex.TotalScore);
            var weighted = score.Components.Sum(c => c.BaseWeight * c.Score!.Value);
            score.TotalScore.ShouldBe(decimal.Round(weighted, 2));
            score.InterpretationLabel.ShouldNotBeNullOrEmpty();

            // El análisis financiero no borra los problemas industriales (y al revés).
            var problems = (await (await client.GetAsync("/api/v1/problems?year=2024&pageSize=100", Ct)).ReadAsync<ProblemPage>()).Items;
            problems.Count(p => IndustrialDiagnosisEngine.Codes.Contains(p.Code)).ShouldBe(4);
            problems.ShouldContain(p => !IndustrialDiagnosisEngine.Codes.Contains(p.Code));
            (await analyst.PostAsync("/api/v1/analysis/run", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
            var again = (await (await client.GetAsync("/api/v1/problems?year=2024&pageSize=100", Ct)).ReadAsync<ProblemPage>()).Items;
            again.Select(p => p.Id).Order().ShouldBe(problems.Select(p => p.Id).Order());
        }
    }

    [Fact]
    public async Task ProblemStatus_IsKeptWhenTheAnalysisRunsAgain()
    {
        var (analyst, client, _) = await CompanyWithIndustrialDataAsync();
        using (analyst)
        using (client)
        {
            var problems = (await (await client.GetAsync("/api/v1/problems?pageSize=100", Ct)).ReadAsync<ProblemPage>()).Items;
            var scrap = problems.Single(p => p.Code == IndustrialDiagnosisEngine.HighScrap);
            (await analyst.PatchAsJsonAsync($"/api/v1/problems/{scrap.Id}", new UpdateProblemStatusRequest(ProblemStatus.Acknowledged), Json.Options, Ct))
                .StatusCode.ShouldBe(HttpStatusCode.OK);

            // Volver a subir la producción reanaliza: el problema sigue siendo el mismo y conserva su estado.
            await UploadAsync(analyst, "produccion.xlsx");
            var after = (await (await client.GetAsync("/api/v1/problems?pageSize=100", Ct)).ReadAsync<ProblemPage>()).Items;
            var same = after.Single(p => p.Code == IndustrialDiagnosisEngine.HighScrap);
            same.Id.ShouldBe(scrap.Id);
            same.Status.ShouldBe(ProblemStatus.Acknowledged);
            after.Count(p => IndustrialDiagnosisEngine.Codes.Contains(p.Code)).ShouldBe(4);
        }
    }

    [Fact]
    public async Task InvalidRows_AreSkippedWithTheirLocation()
    {
        var (analyst, client, _) = await CompanyAsync();
        using (analyst)
        using (client)
        {
            var report = await UploadAsync(analyst, "produccion_con_errores.csv");
            report.FileType.ShouldBe(UploadFileType.Production);
            report.Errors.ShouldBeEmpty();
            report.RowsCount.ShouldBe(2); // 01/10 (la última fila repetida gana) y 02/10
            var rows = report.Warnings.Where(w => w.Code.StartsWith("upload.industrial_", StringComparison.Ordinal)).ToList();
            rows.Select(w => (w.Row, w.Column)).ShouldBe([(4, "G"), (5, "B"), (6, "A"), (7, "E"), (8, null)]);
            rows[0].Message.ShouldContain("unidades buenas no pueden superar");
            rows[4].Code.ShouldBe("upload.industrial_duplicate");

            // Sin estándares: no hay rendimiento ni OEE, y se avisa.
            report.Warnings.ShouldContain(w => w.Message.Contains("Sin estándar para MAQ-01", StringComparison.Ordinal));
            var kpis = await (await client.GetAsync("/api/v1/industrial/kpis", Ct)).ReadAsync<IndustrialKpisDto>();
            kpis.Summary!.Kpis.Oee.ShouldBeNull();
            kpis.Summary.Kpis.Quality.ShouldNotBeNull();
            kpis.Summary.UnitsProduced.ShouldBe(1660m + 1680m); // sin «unidades producidas» se usan las buenas
        }
    }

    [Fact]
    public async Task ForcedTypeWithoutItsColumns_IsABlockingError()
    {
        var (analyst, client, _) = await CompanyAsync();
        using (analyst)
        using (client)
        {
            var bytes = await File.ReadAllBytesAsync(Path.Combine(UploadClient.SampleDirectory, "industrial", "estandares.xlsx"), Ct);
            var report = await (await analyst.UploadBytesAsync(bytes, "estandares.xlsx", fileType: "Production")).ReadAsync<UploadReportDto>(HttpStatusCode.UnprocessableEntity);
            report.Status.ShouldBe(UploadStatus.Failed);
            report.Errors.Single().Code.ShouldBe("upload.industrial_columns_missing");
            report.Errors.Single().Message.ShouldContain("Minutos planificados");
        }
    }

    [Fact]
    public async Task WithoutIndustrialData_HasDataIsFalse_AndEveryRoleCanQuery()
    {
        var (analyst, client, _) = await CompanyAsync();
        using (analyst)
        using (client)
        {
            var kpis = await (await client.GetAsync("/api/v1/industrial/kpis", Ct)).ReadAsync<IndustrialKpisDto>();
            kpis.HasData.ShouldBeFalse();
            kpis.Summary.ShouldBeNull();
            kpis.ByMachine.ShouldBeEmpty();
            var score = await (await analyst.GetAsync("/api/v1/industrial/score", Ct)).ReadAsync<IndustrialScoreDto>();
            score.HasData.ShouldBeFalse();
            score.TotalScore.ShouldBeNull();
            score.Message.ShouldNotBeNull();

            using var anonymous = factory.CreateClient();
            (await anonymous.GetAsync("/api/v1/industrial/kpis", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }

    [Fact]
    public async Task Admin_NeedsToChooseTheCompany()
    {
        var (admin, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        using (admin)
        {
            await (await admin.GetAsync("/api/v1/industrial/kpis", Ct)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "industrial.company_required");
        }
    }

    [Theory]
    [InlineData("Production", "plantilla_produccion.xlsx")]
    [InlineData("Costs", "plantilla_costos.xlsx")]
    [InlineData("Standards", "plantilla_estandares.xlsx")]
    public async Task Templates_CanBeDownloaded_AndUploadedBack(string fileType, string fileName)
    {
        var (analyst, client, _) = await CompanyAsync();
        using (analyst)
        using (client)
        {
            var response = await analyst.GetAsync($"/api/v1/uploads/templates/{fileType}", Ct);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.Content.Headers.ContentDisposition!.FileNameStar.ShouldBe(fileName);
            var bytes = await response.Content.ReadAsByteArrayAsync(Ct);

            // La plantilla con sus filas de ejemplo se reconoce sola (sin indicar el tipo).
            var report = await (await analyst.UploadBytesAsync(bytes, fileName)).ReadAsync<UploadReportDto>(HttpStatusCode.Created);
            report.FileType.ToString().ShouldBe(fileType);
            report.Errors.ShouldBeEmpty();
            report.RowsCount.ShouldBe(2);
            report.Sheets.Single(s => s.Name == "Instrucciones").Ignored.ShouldBeTrue();
        }
    }
}
