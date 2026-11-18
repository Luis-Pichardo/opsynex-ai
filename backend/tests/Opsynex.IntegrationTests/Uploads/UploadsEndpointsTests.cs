using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Opsynex.Application.Uploads;
using Opsynex.Domain.Enums;
using Opsynex.IntegrationTests.Infrastructure;

namespace Opsynex.IntegrationTests.Uploads;

public sealed class UploadsEndpointsTests(OpsynexApiFactory factory) : IClassFixture<OpsynexApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DatabaseFixture Db => factory.Database;

    private static readonly JsonDocument Expected =
        JsonDocument.Parse(File.ReadAllText(Path.Combine(UploadClient.SampleDirectory, "expected_values.json")));

    private static decimal ExpectedValue(string file, int year, string key) =>
        Expected.RootElement.GetProperty(file).GetProperty("values").GetProperty(year.ToString(System.Globalization.CultureInfo.InvariantCulture)).GetProperty(key).GetDecimal();

    [Theory]
    [InlineData("libro_sintetico_2a.xls")]
    [InlineData("libro_sintetico_2a.xlsx")]
    public async Task Analyst_UploadsWorkbook_StoresRawValuesAndReport(string file)
    {
        var (client, analyst) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst);
        using (client)
        {
            using var response = await client.UploadSampleAsync(file);
            var report = await response.ReadAsync<UploadReportDto>(HttpStatusCode.Created);

            report.Status.ShouldBe(UploadStatus.Validated);
            report.FileType.ShouldBe(UploadFileType.AccountingWorkbook);
            report.CompanyId.ShouldBe(analyst.CompanyId!.Value);
            report.Years.ShouldBe([2023, 2024]);
            report.Errors.ShouldBeEmpty();
            report.Warnings.ShouldBeEmpty();
            report.Sheets.Single(s => s.Name == "g&P").DetectedType.ShouldBe(SheetType.IncomeStatement);
            report.Sheets.Single(s => s.Name == "ANEXOA1").Ignored.ShouldBeTrue();
            report.Summary.EmployeeCount.ShouldBe(15);
            report.Summary.MonthlyRecords.ShouldBe(12);
            report.Summary.TrialBalanceReliable.ShouldBe(true);
            report.Preview.Lines.ShouldContain(l => l.Label == "Ventas netas" && l.Values[2024] == ExpectedValue(file, 2024, "gross_sales"));
            response.Headers.Location!.ToString().ShouldEndWith($"/api/v1/uploads/{report.UploadId}");

            await using var context = Db.CreateContext();
            var raw = await context.RawAccountValues.Where(r => r.UploadId == report.UploadId).ToListAsync(Ct);
            raw.Count.ShouldBe(report.RowsCount);
            raw.Single(r => r.SheetName == "g&P" && r.NormalizedLabel == "Ventas netas" && r.Year == 2024).Value
                .ShouldBe(ExpectedValue(file, 2024, "gross_sales"));
            raw.Single(r => r.SheetName == "g&P" && r.NormalizedLabel == "Descuentos sobre ventas" && r.Year == 2023).Value
                .ShouldBe(ExpectedValue(file, 2023, "discounts"));
            raw.Single(r => r.SheetName == "g&P" && r.NormalizedLabel == "Costos de Ventas" && r.Year == 2024).Value
                .ShouldBe(ExpectedValue(file, 2024, "cogs"));
            var totalAssets = raw.Single(r => r.SheetName == "BALANCE" && r.NormalizedLabel == "Total Activos" && r.Year == 2024);
            totalAssets.Value.ShouldBe(ExpectedValue(file, 2024, "total_assets"));
            totalAssets.RowKind.ShouldBe(RowKind.Total);
            raw.Single(r => r.SheetName == "BALANCE" && r.NormalizedLabel == "Total capital" && r.Year == 2023).Value
                .ShouldBe(ExpectedValue(file, 2023, "equity"));
            var cash = raw.Single(r => r.SheetName == "BALANCE" && r.NormalizedLabel == "Efectivo en Cajas & Bancos" && r.Year == 2024);
            cash.OriginalLabel.ShouldBe("Efectivo en Cajas & Bancos (nota 3)");
            cash.Section.ShouldBe("ACTIVOS CORRIENTES");
            cash.SheetType.ShouldBe(SheetType.BalanceSheet);
            raw.ShouldContain(r => r.SheetType == SheetType.TrialBalance && r.AccountCode == "4101");

            (await context.MonthlyFinancialData.CountAsync(m => m.CompanyId == analyst.CompanyId && m.Year == 2024, Ct)).ShouldBe(12);
            (await context.OperationalData.SingleAsync(o => o.CompanyId == analyst.CompanyId && o.Year == 2024, Ct)).EmployeeCount.ShouldBe(15);
        }
    }

    [Fact]
    public async Task Upload_NeverStoresEmployeeNamesOrIds()
    {
        var (client, analyst) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst);
        using (client)
        {
            using var response = await client.UploadSampleAsync("libro_sintetico_2a.xls");
            var body = await response.Content.ReadAsStringAsync(Ct);
            response.StatusCode.ShouldBe(HttpStatusCode.Created);

            await using var context = Db.CreateContext();
            var raw = await context.RawAccountValues.Where(r => r.CompanyId == analyst.CompanyId).ToListAsync(Ct);
            var upload = await context.Uploads.SingleAsync(u => u.CompanyId == analyst.CompanyId, Ct);
            var stored = string.Join("\n", raw.Select(r => $"{r.OriginalLabel}|{r.NormalizedLabel}|{r.Section}|{r.ClientAccount}"))
                + JsonSerializer.Serialize(upload.Sheets) + JsonSerializer.Serialize(upload.Summary)
                + JsonSerializer.Serialize(upload.Warnings) + body;

            stored.ShouldNotContain("EMPLEADO 0", Case.Sensitive);
            stored.ShouldNotContain("000-0000000-0");
            stored.ShouldNotContain("000-00000-0");
            stored.ShouldNotContain("000-000001-1");
            stored.ShouldNotContain("FICTICIAS", Case.Sensitive); // nombre de la empresa del encabezado
            raw.ShouldNotContain(r => r.SheetType == SheetType.EmployeeRoster);
            upload.Summary.EmployeeCount.ShouldBe(15);
            upload.Summary.EmployeesByDepartment.Sum(d => d.Employees).ShouldBe(15);
        }
    }

    [Theory]
    [InlineData("inc1_balance_no_cuadra.xls", "quality.balance_mismatch", "BALANCE")]
    [InlineData("inc2_nota_no_cuadra.xls", "quality.note_mismatch", "nota1")]
    [InlineData("inc3_patrimonio.xls", "quality.equity_mismatch", "EPC")]
    [InlineData("inc4_comprobacion.xls", "quality.trial_balance_unbalanced", "BCE COMPROBA")]
    [InlineData("inc5_nombre_empresa.xls", "quality.company_name_mismatch", "nota3")]
    [InlineData("inc_subtotal.xls", "quality.subtotal_mismatch", "g&P")]
    public async Task Inconsistencies_AreWarningsAndDoNotBlock(string file, string code, string sheet)
    {
        var (client, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst);
        using (client)
        {
            var report = await (await client.UploadSampleAsync(file)).ReadAsync<UploadReportDto>(HttpStatusCode.Created);

            report.Status.ShouldBe(UploadStatus.Validated);
            var warning = report.Warnings.First(w => w.Code == code);
            warning.Sheet.ShouldBe(sheet);
            warning.Message.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task BrokenTrialBalance_IsFlaggedAsUnreliable()
    {
        var (client, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst);
        using (client)
        {
            var report = await (await client.UploadSampleAsync("inc4_comprobacion.xls")).ReadAsync<UploadReportDto>(HttpStatusCode.Created);

            report.Summary.TrialBalanceReliable.ShouldBe(false);
        }
    }

    [Theory]
    [InlineData("invalido_sin_estados.xlsx", "upload.no_financial_statements")]
    [InlineData("invalido_anios.xlsx", "upload.years_not_found")]
    [InlineData("invalido_vacio.xlsx", "upload.empty_file")]
    [InlineData("invalido_extension_falsa.xls", "upload.invalid_signature")]
    [InlineData("invalido_corrupto.xlsx", "upload.unreadable")]
    [InlineData("invalido_corrupto.xls", "upload.unreadable")]
    public async Task InvalidFiles_Return422WithBlockingErrorAndAreRecorded(string file, string code)
    {
        var (client, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst);
        using (client)
        {
            var report = await (await client.UploadSampleAsync(file)).ReadAsync<UploadReportDto>(HttpStatusCode.UnprocessableEntity);

            report.Status.ShouldBe(UploadStatus.Failed);
            report.Errors.Select(e => e.Code).ShouldContain(code);
            report.RowsCount.ShouldBe(0);

            var list = await (await client.GetAsync("/api/v1/uploads", Ct)).ReadAsync<List<UploadListItemDto>>();
            list.ShouldHaveSingleItem().Status.ShouldBe(UploadStatus.Failed);
        }
    }

    [Fact]
    public async Task Client_CannotUpload()
    {
        var (client, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Client);
        using (client)
        {
            using var response = await client.UploadSampleAsync("libro_sintetico_2a.xls");

            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
        }
    }

    [Fact]
    public async Task Admin_MustIndicateCompany()
    {
        var companyId = await TestUsers.CreateCompanyAsync(Db);
        var (admin, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        using (admin)
        {
            using var withoutCompany = await admin.UploadSampleAsync("libro_sintetico_2a.xls");
            await withoutCompany.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "upload.company_required");

            using var unknown = await admin.UploadSampleAsync("libro_sintetico_2a.xls", Guid.NewGuid());
            await unknown.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "upload.company_not_found");

            var report = await (await admin.UploadSampleAsync("libro_sintetico_2a.xls", companyId)).ReadAsync<UploadReportDto>(HttpStatusCode.Created);
            report.CompanyId.ShouldBe(companyId);
        }
    }

    [Fact]
    public async Task Upload_RejectsMissingFileAndUnsupportedExtension()
    {
        var (client, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst);
        using (client)
        {
            using var form = new MultipartFormDataContent { { new StringContent("IncomeStatement"), "fileType" } };
            using var noFile = await client.PostAsync("/api/v1/uploads", form, Ct);
            await noFile.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "upload.file_required");

            using var txt = await client.UploadBytesAsync("hola"u8.ToArray(), "datos.txt");
            await txt.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "upload.unsupported_extension");
        }
    }

    [Fact]
    public async Task CsvTemplate_RequiresFileType()
    {
        var (client, analyst) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst);
        using (client)
        {
            var failed = await (await client.UploadSampleAsync("plantilla_estado_resultados.csv")).ReadAsync<UploadReportDto>(HttpStatusCode.UnprocessableEntity);
            failed.Errors.Single().Code.ShouldBe("upload.file_type_required");

            var report = await (await client.UploadSampleAsync("plantilla_estado_resultados.csv", fileType: "IncomeStatement"))
                .ReadAsync<UploadReportDto>(HttpStatusCode.Created);
            report.FileType.ShouldBe(UploadFileType.IncomeStatement);
            report.Years.ShouldBe([2023, 2024]);
            report.Preview.Lines.Single(l => l.Label == "Ventas netas").Values[2023]
                .ShouldBe(ExpectedValue("libro_sintetico_2a.xls", 2023, "gross_sales"));
        }
    }

    [Fact]
    public async Task OperationalTemplate_FillsOperationalData()
    {
        var (client, analyst) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst);
        using (client)
        {
            var report = await (await client.UploadSampleAsync("plantilla_operacional.xlsx")).ReadAsync<UploadReportDto>(HttpStatusCode.Created);
            report.FileType.ShouldBe(UploadFileType.Operational);

            await using var context = Db.CreateContext();
            var data = await context.OperationalData.Where(o => o.CompanyId == analyst.CompanyId).OrderBy(o => o.Year).ToListAsync(Ct);
            data.Select(o => o.Year).ShouldBe([2023, 2024]);
            data[1].UnitsProduced.ShouldBe(1_240_000m);
            data[1].EmployeeCount.ShouldBe(15);
            data[1].EnergyConsumptionKwh.ShouldBe(2_610_000m);
            data[1].ScrapEstimate.ShouldBe(1_720_000m);
            data[1].OperationalCosts.ShouldBe(28_900_000m);
            data[1].EnergyCost.ShouldBe(11_210_000m);
        }
    }

    [Fact]
    public async Task ReUpload_ReplacesMonthlySeriesWithoutDuplicates()
    {
        var (client, analyst) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst);
        using (client)
        {
            (await client.UploadSampleAsync("libro_sintetico_2a.xls")).StatusCode.ShouldBe(HttpStatusCode.Created);
            (await client.UploadSampleAsync("libro_sintetico_2a.xlsx")).StatusCode.ShouldBe(HttpStatusCode.Created);

            await using var context = Db.CreateContext();
            (await context.MonthlyFinancialData.CountAsync(m => m.CompanyId == analyst.CompanyId, Ct)).ShouldBe(12);
            (await context.Uploads.CountAsync(u => u.CompanyId == analyst.CompanyId, Ct)).ShouldBe(2);
        }
    }

    [Fact]
    public async Task ListAndGet_ReturnReportsOfOwnCompanyOnly()
    {
        var (analyst, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst);
        var (other, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Client);
        using (analyst)
        using (other)
        {
            var created = await (await analyst.UploadSampleAsync("libro_sintetico_2a.xls")).ReadAsync<UploadReportDto>(HttpStatusCode.Created);

            var list = await (await analyst.GetAsync("/api/v1/uploads", Ct)).ReadAsync<List<UploadListItemDto>>();
            list.ShouldHaveSingleItem().Id.ShouldBe(created.UploadId);

            var report = await (await analyst.GetAsync($"/api/v1/uploads/{created.UploadId}", Ct)).ReadAsync<UploadReportDto>();
            report.Sheets.Count.ShouldBe(created.Sheets.Count);
            report.Summary.EmployeeCount.ShouldBe(15);
            report.Preview.Lines.Count.ShouldBe(created.Preview.Lines.Count);
            report.Preview.Lines[0].Values.Count.ShouldBe(2);

            (await (await other.GetAsync("/api/v1/uploads", Ct)).ReadAsync<List<UploadListItemDto>>()).ShouldBeEmpty();
            using var notFound = await other.GetAsync($"/api/v1/uploads/{created.UploadId}", Ct);
            await notFound.ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
        }
    }

    [Theory]
    [InlineData("IncomeStatement", SheetType.IncomeStatement)]
    [InlineData("BalanceSheet", SheetType.BalanceSheet)]
    [InlineData("Operational", SheetType.Operational)]
    [InlineData("CashFlow", SheetType.CashFlow)]
    [InlineData("CostStructure", SheetType.CostStructure)]
    public async Task Templates_AreValidXlsxRecognizedByTheParser(string fileType, SheetType expected)
    {
        var (client, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst);
        using (client)
        {
            using var response = await client.GetAsync($"/api/v1/uploads/templates/{fileType}", Ct);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.Content.Headers.ContentType!.MediaType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            response.Content.Headers.ContentDisposition!.FileName.ShouldNotBeNull();

            var bytes = await response.Content.ReadAsByteArrayAsync(Ct);
            var parser = new Opsynex.Infrastructure.Parsing.WorkbookParser(
                Microsoft.Extensions.Options.Options.Create(new UploadOptions()), TimeProvider.System);
            var parsed = parser.Parse(new MemoryStream(bytes), "plantilla.xlsx", null, Ct);
            parsed.Sheets.Single().DetectedType.ShouldBe(expected);
            parsed.Years.Count.ShouldBe(2);
        }
    }

    [Fact]
    public async Task Templates_RequireAnalyzeRoleAndKnownType()
    {
        var (client, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Client);
        var (analyst, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst);
        using (client)
        using (analyst)
        {
            await (await client.GetAsync("/api/v1/uploads/templates/IncomeStatement", Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
            await (await analyst.GetAsync("/api/v1/uploads/templates/AccountingWorkbook", Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "not_found");
        }
    }
}

public sealed class UploadSizeLimitTests(SmallUploadLimitApiFactory factory) : IClassFixture<SmallUploadLimitApiFactory>
{
    [Fact]
    public async Task FileOverLimit_Returns413()
    {
        var (client, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst);
        using (client)
        {
            var big = new byte[(int)(1.5 * 1024 * 1024)];
            using var response = await client.UploadBytesAsync(big, "grande.csv");

            await response.ShouldBeProblemAsync(HttpStatusCode.RequestEntityTooLarge, "upload.too_large");
        }
    }
}
