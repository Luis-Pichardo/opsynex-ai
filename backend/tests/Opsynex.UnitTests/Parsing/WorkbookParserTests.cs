using System.Text.Json;
using Opsynex.Application.Uploads;
using Opsynex.Domain.Enums;

namespace Opsynex.UnitTests.Parsing;

/// <summary>Parser completo contra los libros sintéticos (estructura real, valores inventados).</summary>
public sealed class WorkbookParserTests
{
    public static TheoryData<string> MainWorkbooks => ["libro_sintetico_2a.xls", "libro_sintetico_2a.xlsx"];

    [Theory]
    [MemberData(nameof(MainWorkbooks))]
    public void MainWorkbook_IsParsedWithTwoYearsAndNoIssues(string file)
    {
        var result = SampleData.Parse(file);

        result.Errors.ShouldBeEmpty();
        result.Warnings.ShouldBeEmpty(string.Join("\n", result.Warnings.Select(w => $"{w.Code} {w.Sheet} {w.Row}: {w.Message}")));
        result.Years.ShouldBe([2023, 2024]);
        result.FileType.ShouldBe(UploadFileType.AccountingWorkbook);
    }

    [Theory]
    [MemberData(nameof(MainWorkbooks))]
    public void Sheets_AreClassifiedByContent(string file)
    {
        var sheets = SampleData.Parse(file).Sheets.ToDictionary(s => s.Name);

        sheets["BALANCE"].DetectedType.ShouldBe(SheetType.BalanceSheet);
        sheets["g&P"].DetectedType.ShouldBe(SheetType.IncomeStatement);
        sheets["EPC"].DetectedType.ShouldBe(SheetType.EquityStatement);
        foreach (var note in new[] { "nota1", "nota2", "nota3", "nota4" })
        {
            sheets[note].DetectedType.ShouldBe(SheetType.Notes, note);
            sheets[note].Ignored.ShouldBeFalse();
            sheets[note].RowsExtracted.ShouldBeGreaterThan(0);
        }

        sheets["REGPASCUAL"].DetectedType.ShouldBe(SheetType.EmployeeRoster);
        sheets["CUADRO"].DetectedType.ShouldBe(SheetType.MonthlySales);
        sheets["BCE COMPROBA"].DetectedType.ShouldBe(SheetType.TrialBalance);

        foreach (var ignored in new[] { "export", "anticipos", "desc", "CXP", "IMPORT", "PRESTLAB", "ADICIONES", "ANEXOA1", "ANEXOB1", "BONIF." })
        {
            sheets[ignored].Ignored.ShouldBeTrue(ignored);
            sheets[ignored].DetectedType.ShouldBe(SheetType.Unknown, ignored);
            sheets[ignored].Reason.ShouldNotBeNullOrWhiteSpace();
        }

        sheets["BONIF."].Reason!.ShouldContain("vacía");
    }

    public static TheoryData<string, string, string?, string, RowKind> KeyLines => new()
    {
        { "g&P", "Ventas netas", null, "gross_sales", RowKind.Detail },
        { "g&P", "Descuentos sobre ventas", null, "discounts", RowKind.Detail },
        { "g&P", "Costos de Ventas", null, "cogs", RowKind.Detail },
        { "g&P", "Compensaciones al personal", null, "comp_total", RowKind.Detail },
        { "g&P", "Gastos Administrativos", null, "admin_total", RowKind.Detail },
        { "g&P", "Gastos Financieros", null, "fin_exp", RowKind.Detail },
        { "g&P", "Ingresos netos antes impuesto s/renta", null, "ebt", RowKind.Detail },
        { "g&P", "Impuesto sobre la renta", null, "isr", RowKind.Detail },
        { "g&P", "Ingresos Netos", null, "net_income", RowKind.Detail },
        { "g&P", "Total gastos de operaciones", null, "opex_total", RowKind.Subtotal },
        { "BALANCE", "Efectivo en Cajas & Bancos", null, "cash", RowKind.Detail },
        { "BALANCE", "Cuentas por Cobrar", null, "cxc_total", RowKind.Detail },
        { "BALANCE", "Inventario Materia Prima", null, "inv_fin_mp", RowKind.Detail },
        { "BALANCE", "Total corriente", "ACTIVOS", "current_assets", RowKind.Subtotal },
        { "BALANCE", "Total corriente", "PASIVO", "pasivo_corriente", RowKind.Subtotal },
        { "BALANCE", "Total Activos", null, "total_assets", RowKind.Total },
        { "BALANCE", "Préstamos a largo plazo", null, "prest_lp", RowKind.Detail },
        { "BALANCE", "Total capital", null, "equity", RowKind.Subtotal },
        { "BALANCE", "Total Pasivos & Capital", null, "total_liab_equity", RowKind.Total },
        { "nota3", "Energía Eléctrica", "13.", "g_energia", RowKind.Detail },
        { "nota3", "Total compensaciones al personal", null, "comp_total", RowKind.Subtotal },
        { "nota4", "Energía Eléctrica", null, "fab_energia", RowKind.Detail },
        { "nota4", "Depreciación Maquinarias & Equipos", null, "fab_dep", RowKind.Detail },
        { "nota4", "Materias primas importadas", null, "mp_import", RowKind.Detail },
        { "nota4", "Compras de materias primas locales", null, "mp_local", RowKind.Detail },
        { "nota1", "Depreciación acumulada", null, "dep_acum", RowKind.Detail },
    };

    [Theory]
    [MemberData(nameof(KeyLines))]
    public void RawValues_MatchGeneratorValues(string sheet, string label, string? section, string key, RowKind kind)
    {
        foreach (var file in new[] { "libro_sintetico_2a.xls", "libro_sintetico_2a.xlsx" })
        {
            var line = SampleData.Parse(file).Line(sheet, label, section);

            line.RowKind.ShouldBe(kind, $"{file} {sheet} {label}");
            foreach (var year in new[] { 2023, 2024 })
            {
                line.Values[year].ShouldBe(SampleData.Value(file, year, key), $"{file} {sheet} «{label}» {year}");
            }
        }
    }

    [Fact]
    public void Labels_KeepOriginalTextAndNormalizedVersion()
    {
        var line = SampleData.Parse("libro_sintetico_2a.xls").Line("BALANCE", "Efectivo en Cajas & Bancos");

        line.OriginalLabel.ShouldBe("Efectivo en Cajas & Bancos (nota 3)");
        line.Section.ShouldBe("ACTIVOS CORRIENTES");
        line.RowNumber.ShouldBeGreaterThan(5);
    }

    [Fact]
    public void Employees_AreCountedWithoutPersonalData()
    {
        var result = SampleData.Parse("libro_sintetico_2a.xls");
        var expected = SampleData.Expected("libro_sintetico_2a.xls").GetProperty("employees");

        result.Employees.ShouldNotBeNull();
        result.Employees.Count.ShouldBe(expected.GetProperty("count").GetInt32());
        result.Employees.Year.ShouldBe(2024);
        result.Employees.Payroll.ShouldBe(expected.GetProperty("payroll").GetDecimal());
        result.Employees.ByDepartment.Select(d => (d.Department, d.Employees))
            .ShouldBe([("Produccion", 8), ("Administracion", 4), ("Ventas", 3)]);

        // Ningún nombre, cédula ni número de cuenta sale del parser.
        var json = JsonSerializer.Serialize(result);
        json.ShouldNotContain("EMPLEADO 0");
        json.ShouldNotContain("000-0000000-0");
        json.ShouldNotContain("000-000001-1");
        json.ShouldNotContain("000-00000-0");
        result.Lines.ShouldNotContain(l => l.SheetType == SheetType.EmployeeRoster);
    }

    [Fact]
    public void BankAccountNumbers_AreMaskedInLabels()
    {
        var result = SampleData.Parse("libro_sintetico_2a.xls");

        result.Lines.ShouldContain(l => l.SheetName == "nota1" && l.OriginalLabel == "Banco Ficticio Comercial, Cta. ***");
    }

    [Fact]
    public void MonthlySales_AreExtracted()
    {
        var result = SampleData.Parse("libro_sintetico_2a.xls");
        var expected = SampleData.Expected("libro_sintetico_2a.xls").GetProperty("monthly");
        var revenue = expected.GetProperty("revenue").EnumerateArray().Select(v => v.GetDecimal()).ToList();
        var purchases = expected.GetProperty("purchases").EnumerateArray().Select(v => v.GetDecimal()).ToList();

        result.Monthly.Count.ShouldBe(12);
        result.Monthly.ShouldAllBe(m => m.Year == 2024);
        result.Monthly.Select(m => m.Revenue!.Value).ShouldBe(revenue);
        result.Monthly.Select(m => m.Purchases!.Value).ShouldBe(purchases);
        result.Monthly.Sum(m => m.Revenue!.Value).ShouldBe(SampleData.Value("libro_sintetico_2a.xls", 2024, "gross_sales"));
    }

    [Fact]
    public void TrialBalance_IsReliableAndKeepsAccountCodes()
    {
        var result = SampleData.Parse("libro_sintetico_2a.xls");

        result.TrialBalance.ShouldNotBeNull();
        result.TrialBalance.Reliable.ShouldBeTrue();
        result.TrialBalance.Difference.ShouldBe(0m);
        result.TrialBalance.Year.ShouldBe(2024);
        var sales = result.Lines.Single(l => l.SheetType == SheetType.TrialBalance && l.AccountCode == "4101");
        sales.NormalizedLabel.ShouldBe("Ventas");
        sales.RowKind.ShouldBe(RowKind.Detail);
        sales.Values[2024].ShouldBe(SampleData.Value("libro_sintetico_2a.xls", 2024, "gross_sales"));
        result.Lines.Single(l => l.SheetType == SheetType.TrialBalance && l.AccountCode == "4").RowKind.ShouldBe(RowKind.Subtotal);
    }

    [Theory]
    [InlineData("libro_sintetico_3a.xls")]
    [InlineData("libro_sintetico_3a.xlsx")]
    public void ThreeYearWorkbook_DetectsThreeYears(string file)
    {
        var result = SampleData.Parse(file);

        result.Errors.ShouldBeEmpty();
        result.Warnings.ShouldBeEmpty(string.Join("\n", result.Warnings.Select(w => w.Message)));
        result.Years.ShouldBe([2022, 2023, 2024]);
        result.Line("g&P", "Ventas netas").Values[2022].ShouldBe(SampleData.Value(file, 2022, "gross_sales"));
    }

    [Theory]
    [InlineData("inc1_balance_no_cuadra.xls", "quality.balance_mismatch", "BALANCE")]
    [InlineData("inc2_nota_no_cuadra.xls", "quality.note_mismatch", "nota1")]
    [InlineData("inc3_patrimonio.xls", "quality.equity_mismatch", "EPC")]
    [InlineData("inc4_comprobacion.xls", "quality.trial_balance_unbalanced", "BCE COMPROBA")]
    [InlineData("inc4_comprobacion.xls", "quality.trial_balance_negative_balance", "BCE COMPROBA")]
    [InlineData("inc4_comprobacion.xls", "quality.trial_balance_sales_mismatch", "BCE COMPROBA")]
    [InlineData("inc5_nombre_empresa.xls", "quality.company_name_mismatch", "nota3")]
    [InlineData("inc_subtotal.xls", "quality.subtotal_mismatch", "g&P")]
    [InlineData("inc_anio_faltante.xls", "quality.missing_year", "g&P")]
    public void Inconsistencies_AreReportedAsWarningsWithLocation(string file, string code, string sheet)
    {
        var result = SampleData.Parse(file);

        result.Errors.ShouldBeEmpty();
        var warning = result.Warnings.FirstOrDefault(w => w.Code == code);
        warning.ShouldNotBeNull($"{file}: {string.Join(", ", result.Warnings.Select(w => w.Code))}");
        warning.Sheet.ShouldBe(sheet);
        warning.Message.ShouldNotBeNullOrWhiteSpace();
        if (code is not ("quality.trial_balance_unbalanced" or "quality.trial_balance_sales_mismatch" or "quality.missing_year"))
        {
            warning.Row.ShouldNotBeNull();
        }

        // Solo se reportan las inconsistencias que tiene cada archivo.
        var expectedCodes = SampleData.Expected(file).GetProperty("expectedWarnings").EnumerateArray().Select(c => c.GetString()).ToHashSet();
        result.Warnings.Select(w => w.Code).ShouldAllBe(c => expectedCodes.Contains(c));
    }

    [Fact]
    public void BrokenTrialBalance_IsMarkedUnreliable()
    {
        var result = SampleData.Parse("inc4_comprobacion.xls");

        result.TrialBalance!.Reliable.ShouldBeFalse();
        result.TrialBalance.Difference.ShouldNotBe(0m);
    }

    [Theory]
    [InlineData("invalido_sin_estados.xlsx", "upload.no_financial_statements")]
    [InlineData("invalido_anios.xlsx", "upload.years_not_found")]
    [InlineData("invalido_vacio.xlsx", "upload.empty_file")]
    [InlineData("invalido_extension_falsa.xls", "upload.invalid_signature")]
    [InlineData("invalido_corrupto.xlsx", "upload.unreadable")]
    [InlineData("invalido_corrupto.xls", "upload.unreadable")]
    public void InvalidFiles_ProduceBlockingErrors(string file, string code)
    {
        var result = SampleData.Parse(file);

        result.HasBlockingErrors.ShouldBeTrue();
        result.Errors.Select(e => e.Code).ShouldContain(code);
        SampleData.Expected(file).GetProperty("blockingError").GetString().ShouldBe(code);
    }

    [Fact]
    public void CsvTemplate_WithFileType_IsParsed()
    {
        var result = SampleData.Parse("plantilla_estado_resultados.csv", UploadFileType.IncomeStatement);

        result.Errors.ShouldBeEmpty();
        result.FileType.ShouldBe(UploadFileType.IncomeStatement);
        result.Years.ShouldBe([2023, 2024]);
        result.Sheets.Single().DetectedType.ShouldBe(SheetType.IncomeStatement);
        result.Line("plantilla_estado_resultados", "Ventas netas").Values[2024]
            .ShouldBe(SampleData.Value("libro_sintetico_2a.xls", 2024, "gross_sales"));
    }

    [Fact]
    public void CsvTemplate_WithoutFileType_AsksForIt()
    {
        var result = SampleData.Parse("plantilla_estado_resultados.csv");

        result.Errors.Single().Code.ShouldBe("upload.file_type_required");
    }

    [Fact]
    public void OperationalTemplate_IsRecognizedByTitle()
    {
        var result = SampleData.Parse("plantilla_operacional.xlsx");

        result.Errors.ShouldBeEmpty();
        result.FileType.ShouldBe(UploadFileType.Operational);
        result.Line("Datos operacionales", "Cantidad de empleados").Values[2024].ShouldBe(15m);
    }
}
