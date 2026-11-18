using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Opsynex.Application.Homologation;
using Opsynex.Application.Uploads;
using Opsynex.Domain.Enums;
using Opsynex.IntegrationTests.Infrastructure;
using Opsynex.IntegrationTests.Uploads;

namespace Opsynex.IntegrationTests.Homologation;

/// <summary>De punta a punta con el servicio de IA real: subir → sugerencias → consolidación automática.</summary>
public sealed class HomologationEndToEndTests(AiApiFactory factory) : IClassFixture<AiApiFactory>
{
    private const string Book = "libro_sintetico_2a.xls";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Upload_IsAnalyzedAutomatically_AndFinancialDataMatchesGenerator()
    {
        factory.Ai.RequireAvailable();
        var (client, analyst) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst);
        using (client)
        {
            var report = await (await client.UploadSampleAsync(Book)).ReadAsync<UploadReportDto>(HttpStatusCode.Created);

            report.Status.ShouldBe(UploadStatus.Processed);
            report.AnalysisStatus.ShouldBe(AnalysisStatus.Completed);
            report.NeedsReview.ShouldBeFalse(string.Join(", ", report.Warnings.Select(w => w.Message)));
            report.DoubtfulAccounts.ShouldBe(0);

            await using var context = factory.Database.CreateContext();
            var data = await context.FinancialData.Where(f => f.CompanyId == analyst.CompanyId).OrderBy(f => f.Year).ToListAsync(Ct);
            data.Select(d => d.Year).ShouldBe([2023, 2024]);
            foreach (var row in data)
            {
                var mismatches = Expected.Compare(row, Book, row.Year).Where(c => c.Actual != c.Expected).ToList();
                mismatches.ShouldBeEmpty(string.Join("\n", mismatches.Select(m => $"{row.Year} {m.Field}: {m.Actual} ≠ {m.Expected}")));
            }
        }
    }

    [Fact]
    public async Task NotesAndStatements_AreNotDoubleCounted()
    {
        factory.Ai.RequireAvailable();
        var (client, analyst) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst);
        using (client)
        {
            (await client.UploadSampleAsync(Book)).StatusCode.ShouldBe(HttpStatusCode.Created);

            await using var context = factory.Database.CreateContext();
            var data = await context.FinancialData.SingleAsync(f => f.CompanyId == analyst.CompanyId && f.Year == 2024, Ct);
            var mappings = await context.AccountMappings.Where(m => m.CompanyId == analyst.CompanyId).ToListAsync(Ct);

            // La nota 12 también trae "Costo de ventas" y la nota 3 detalla el efectivo: no se suman al estado.
            mappings.ShouldContain(m => m.Scope == MappingScope.Notes && m.OpsynexCategory == OpsynexCategory.Cogs);
            mappings.ShouldContain(m => m.Scope == MappingScope.Notes && m.OpsynexCategory == OpsynexCategory.Cash);
            data.Cogs.ShouldBe(Expected.V(Book, 2024, "cogs"));
            data.Cash.ShouldBe(Expected.V(Book, 2024, "cash"));
            // Los detalles del capital y su total están mapeados a Equity: se usa el total, una sola vez.
            mappings.Count(m => m.Scope == MappingScope.Statement && m.OpsynexCategory == OpsynexCategory.Equity).ShouldBeGreaterThan(1);
            data.Equity.ShouldBe(Expected.V(Book, 2024, "equity"));
            // La compensación del estado de resultados (opex) no se suma al costo de personal de las notas.
            data.LaborCost.ShouldBe(Expected.Sum(Book, 2024, "comp_total", "labor_total"));
            data.OperatingExpenses.ShouldBe(Expected.V(Book, 2024, "opex_total"));
        }
    }

    public static readonly (MappingScope Scope, string Account, OpsynexCategory Category)[] ExpectedMappings =
    [
        (MappingScope.Statement, "Efectivo en Cajas & Bancos", OpsynexCategory.Cash),
        (MappingScope.Statement, "Cuentas por Cobrar", OpsynexCategory.AccountsReceivable),
        (MappingScope.Statement, "Inventario Productos Terminados", OpsynexCategory.Inventory),
        (MappingScope.Statement, "Inventario Materia Prima", OpsynexCategory.Inventory),
        (MappingScope.Statement, "Total Activos", OpsynexCategory.TotalAssets),
        (MappingScope.Statement, "Préstamos por Pagar", OpsynexCategory.FinancialDebt),
        (MappingScope.Statement, "Cuentas por Pagar", OpsynexCategory.AccountsPayable),
        (MappingScope.Statement, "Préstamos a largo plazo", OpsynexCategory.FinancialDebt),
        (MappingScope.Statement, "Total capital", OpsynexCategory.Equity),
        (MappingScope.Statement, "Total Pasivos & Capital", OpsynexCategory.TotalLiabilitiesAndEquity),
        (MappingScope.Statement, "Ventas netas", OpsynexCategory.GrossSales),
        (MappingScope.Statement, "Descuentos sobre ventas", OpsynexCategory.SalesDiscounts),
        (MappingScope.Statement, "Costos de Ventas", OpsynexCategory.Cogs),
        (MappingScope.Statement, "Compensaciones al personal", OpsynexCategory.OperatingExpenses),
        (MappingScope.Statement, "Gastos Administrativos", OpsynexCategory.OperatingExpenses),
        (MappingScope.Statement, "Depreciación & Amortización", OpsynexCategory.Depreciation),
        (MappingScope.Statement, "Gastos Financieros", OpsynexCategory.FinancialExpenses),
        (MappingScope.Statement, "Ingresos netos antes impuesto s/renta", OpsynexCategory.EarningsBeforeTax),
        (MappingScope.Statement, "Impuesto sobre la renta", OpsynexCategory.IncomeTax),
        (MappingScope.Statement, "Ingresos Netos", OpsynexCategory.NetIncome),
        (MappingScope.Notes, "Energía Eléctrica", OpsynexCategory.EnergyCost),
        (MappingScope.Notes, "Reparaciones", OpsynexCategory.MaintenanceCost),
        (MappingScope.Notes, "Mantenimientos", OpsynexCategory.MaintenanceCost),
        (MappingScope.Notes, "Reparaciones & Mantenimiento Vehículos", OpsynexCategory.MaintenanceCost),
        (MappingScope.Notes, "Reparaciones & mantenimientos", OpsynexCategory.MaintenanceCost),
        (MappingScope.Notes, "Total compensaciones al personal", OpsynexCategory.LaborCost),
        (MappingScope.Notes, "Total labor directa", OpsynexCategory.LaborCost),
        (MappingScope.Notes, "Sueldos", OpsynexCategory.LaborCost),
        (MappingScope.Notes, "Regalía Pascual", OpsynexCategory.LaborCost),
        (MappingScope.Notes, "Materias primas importadas", OpsynexCategory.RawMaterials),
        (MappingScope.Notes, "Compras de materias primas locales", OpsynexCategory.RawMaterials),
        (MappingScope.Notes, "Depreciación Maquinarias & Equipos", OpsynexCategory.Depreciation),
        (MappingScope.Notes, "Total gastos generales y administrativos", OpsynexCategory.OverheadCost),
        (MappingScope.Notes, "Honorarios profesionales", OpsynexCategory.OverheadCost),
    ];

    [Fact]
    public async Task AtLeast90PercentOfKeyLines_GetACorrectSuggestionWithHighConfidence()
    {
        factory.Ai.RequireAvailable();
        var (client, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst);
        using (client)
        {
            (await client.UploadSampleAsync(Book)).StatusCode.ShouldBe(HttpStatusCode.Created);
            var mappings = await (await client.GetAsync("/api/v1/mappings", Ct)).ReadAsync<List<MappingDto>>();

            var results = ExpectedMappings.Select(e =>
            {
                var m = mappings.FirstOrDefault(x => x.Scope == e.Scope && x.ClientAccount == e.Account);
                return (e, m, Ok: m is not null && m.Category == e.Category && m.Confidence >= 0.8m);
            }).ToList();

            var failures = results.Where(r => !r.Ok).Select(r => $"{r.e.Account}: {r.m?.Category} {r.m?.Confidence}").ToList();
            ((decimal)results.Count(r => r.Ok) / results.Count).ShouldBeGreaterThanOrEqualTo(0.9m, string.Join("\n", failures));
            failures.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task UnknownAccounts_MarkReview_AndConfirmedMappingsAreReused()
    {
        factory.Ai.RequireAvailable();
        const string file = "inc_cuentas_desconocidas.xls";
        var (client, analyst) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst);
        using (client)
        {
            var first = await (await client.UploadSampleAsync(file)).ReadAsync<UploadReportDto>(HttpStatusCode.Created);
            first.Status.ShouldBe(UploadStatus.Processed);
            first.NeedsReview.ShouldBeTrue();
            first.DoubtfulAccounts.ShouldBe(2);
            first.Warnings.Count(w => w.Code == "mapping.unmapped_account").ShouldBe(2);
            var unmapped = first.Warnings.Where(w => w.Code == "mapping.unmapped_account").ToList();
            unmapped[0].Message.ShouldContain("Partida especial ZQX"); // ordenadas por hoja y fila
            unmapped[0].Message.ShouldContain("125,000.00");
            unmapped[0].Row.ShouldNotBeNull();

            var pending = await (await client.GetAsync($"/api/v1/mappings/pending?uploadId={first.UploadId}", Ct)).ReadAsync<List<PendingAccountDto>>();
            var doubtful = pending.TakeWhile(p => p.IsDoubtful).ToList();
            doubtful.Select(p => p.ClientAccount).ShouldBe(["Partida especial ZQX", "Ajuste extraordinario QWV"], ignoreOrder: true);
            doubtful.ShouldAllBe(p => p.Category == OpsynexCategory.Other && p.Role == MappingRole.Primary && p.SheetName == "g&P");

            var saved = await (await client.PutAsJsonAsync("/api/v1/mappings",
                new SaveMappingsRequest(doubtful.Select(p => new MappingUpdate(p.MappingId, OpsynexCategory.OperatingExpenses)).ToList()),
                Json.Options, Ct)).ReadAsync<List<MappingDto>>();
            saved.ShouldAllBe(m => m.IsConfirmed && m.Method == MappingMethod.Manual && m.Confidence == 1m);

            var reanalyzed = await (await client.PostAsync($"/api/v1/mappings/consolidate?uploadId={first.UploadId}", null, Ct)).ReadAsync<ProcessingResult>();
            reanalyzed.NeedsReview.ShouldBeFalse();
            reanalyzed.Status.ShouldBe(UploadStatus.Processed);

            // Siguiente carga de la misma empresa: los mapeos confirmados se reutilizan.
            var second = await (await client.UploadSampleAsync(file)).ReadAsync<UploadReportDto>(HttpStatusCode.Created);
            second.NeedsReview.ShouldBeFalse();
            second.Warnings.ShouldNotContain(w => w.Code == "mapping.unmapped_account");

            await using var context = factory.Database.CreateContext();
            var reused = await context.AccountMappings
                .Where(m => m.CompanyId == analyst.CompanyId && m.ClientAccount == "Partida especial ZQX").ToListAsync(Ct);
            reused.ShouldHaveSingleItem().Method.ShouldBe(MappingMethod.Manual);
        }
    }

    [Fact]
    public async Task CorrectingAMapping_AndReanalyzing_UpdatesFinancialData()
    {
        factory.Ai.RequireAvailable();
        var (client, analyst) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst);
        using (client)
        {
            var upload = await (await client.UploadSampleAsync(Book)).ReadAsync<UploadReportDto>(HttpStatusCode.Created);
            var mappings = await (await client.GetAsync("/api/v1/mappings", Ct)).ReadAsync<List<MappingDto>>();
            var energy = mappings.Single(m => m.Scope == MappingScope.Notes && m.ClientAccount == "Energía Eléctrica");
            energy.Category.ShouldBe(OpsynexCategory.EnergyCost);

            using (var put = await client.PutAsJsonAsync("/api/v1/mappings",
                new SaveMappingsRequest([new MappingUpdate(energy.Id, OpsynexCategory.MaintenanceCost)]), Json.Options, Ct))
            {
                put.StatusCode.ShouldBe(HttpStatusCode.OK);
            }

            (await client.PostAsync($"/api/v1/mappings/consolidate?uploadId={upload.UploadId}", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

            await using var context = factory.Database.CreateContext();
            var data = await context.FinancialData.SingleAsync(f => f.CompanyId == analyst.CompanyId && f.Year == 2024, Ct);
            data.EnergyCost.ShouldBeNull();
            data.MaintenanceCost.ShouldBe(Expected.Sum(Book, 2024, "g_rep", "g_mant", "g_rep_veh", "fab_mant", "g_energia", "fab_energia"));
        }
    }

    [Fact]
    public async Task HealthReportsAiServiceAsHealthy()
    {
        factory.Ai.RequireAvailable();
        using var client = factory.CreateClient();

        var body = await client.GetStringAsync(new Uri("/health", UriKind.Relative), Ct);

        body.ShouldContain("\"name\":\"ai-service\",\"status\":\"Healthy\"");
    }
}
