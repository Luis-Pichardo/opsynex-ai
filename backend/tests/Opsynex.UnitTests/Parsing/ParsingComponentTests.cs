using Opsynex.Domain.Catalogs;
using Opsynex.Domain.Enums;
using Opsynex.Infrastructure.Parsing;

namespace Opsynex.UnitTests.Parsing;

public sealed class TextNormalizerTests
{
    [Theory]
    [InlineData("Cuentas por Cobrar (nota 4)", "Cuentas por Cobrar")]
    [InlineData("Menos: Descuentos sobre ventas", "Descuentos sobre ventas")]
    [InlineData("más: Compras locales (9.1)", "Compras locales")]
    [InlineData("  Gastos   Administrativos  nota 13 ", "Gastos Administrativos")]
    [InlineData("Inventario materia prima:", "Inventario materia prima")]
    [InlineData("", "")]
    public void CleanLabel_RemovesNotesPrefixesAndSpaces(string original, string expected) =>
        TextNormalizer.CleanLabel(original).ShouldBe(expected);

    [Theory]
    [InlineData("Energía Eléctrica", "energia electrica")]
    [InlineData("Reparaciones & Mantenimiento", "reparaciones y mantenimiento")]
    [InlineData("ESTADO DE SITUACIÓN", "estado de situacion")]
    public void Key_IsLowercaseWithoutAccents(string text, string expected) => TextNormalizer.Key(text).ShouldBe(expected);

    [Theory]
    [InlineData(1234.5, 1234.5)]
    [InlineData("1,234.56", 1234.56)]
    [InlineData("(1,234)", -1234)]
    [InlineData("RD$ 1,234.00", 1234)]
    [InlineData("-500", -500)]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("-", 0)]
    public void TryParseNumber_ReadsAccountingFormats(object cell, double expected)
    {
        TextNormalizer.TryParseNumber(cell, out var value).ShouldBeTrue();
        value.ShouldBe((decimal)expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Total")]
    [InlineData("12abc")]
    public void TryParseNumber_EmptyOrTextIsNotANumber(object? cell) =>
        TextNormalizer.TryParseNumber(cell, out _).ShouldBeFalse();

    [Theory]
    [InlineData("Cuentas por Cobrar (nota 4)", "4")]
    [InlineData("Préstamos (9.1)", "9.1")]
    [InlineData("Ventas netas", null)]
    public void NoteNumber_IsReadFromLabel(string label, string? expected) => TextNormalizer.NoteNumber(label).ShouldBe(expected);

    [Theory]
    [InlineData("4. CUENTAS POR COBRAR", "4")]
    [InlineData("Nota 12 - Costo de producción", "12")]
    [InlineData("CUENTAS POR COBRAR", null)]
    public void LeadingNoteNumber_IsReadFromNoteTitle(string title, string? expected) =>
        TextNormalizer.LeadingNoteNumber(title).ShouldBe(expected);
}

public sealed class SensitiveDataScrubberTests
{
    [Theory]
    [InlineData("Banco Ficticio, Cta. 000-000001-1", "Banco Ficticio, Cta. ***")]
    [InlineData("Cédula 001-1234567-8", "Cédula ***")]
    [InlineData("RNC 1-01-12345-6", "RNC ***")]
    [InlineData("Retenciones IR-17", "Retenciones IR-17")]
    [InlineData("Tasa del impuesto (27%)", "Tasa del impuesto (27%)")]
    public void Scrub_MasksLongNumbers(string text, string expected) => SensitiveDataScrubber.Scrub(text).ShouldBe(expected);
}

public sealed class SheetClassifierTests
{
    private static SheetGrid Grid(params object?[][] rows) => new("Hoja", rows);

    [Theory]
    [InlineData("BALANCES GENERALES", SheetType.BalanceSheet)]
    [InlineData("Estado de Situación Financiera", SheetType.BalanceSheet)]
    [InlineData("ESTADOS DE GANANCIAS Y PÉRDIDAS", SheetType.IncomeStatement)]
    [InlineData("Estado de Resultados", SheetType.IncomeStatement)]
    [InlineData("Notas a los Estados Financieros", SheetType.Notes)]
    [InlineData("Estado de cambios en el patrimonio", SheetType.EquityStatement)]
    [InlineData("Estado de flujo de efectivo", SheetType.CashFlow)]
    [InlineData("RELACION DE IMPORTACIONES", SheetType.Unknown)]
    public void Classify_ByTitle(string title, SheetType expected)
    {
        var grid = Grid(["EMPRESA DEMO"], [title], [null, null, 2024, 2023]);

        SheetClassifier.Classify(grid).Type.ShouldBe(expected);
    }

    [Fact]
    public void Classify_EmployeeRosterByHeaders() =>
        SheetClassifier.Classify(Grid(["EMPRESA"], ["CODIGO", "NOMBRE", "CEDULA", "CARGO", "ENERO"])).Type.ShouldBe(SheetType.EmployeeRoster);

    [Fact]
    public void Classify_MonthlySalesByMonths() =>
        SheetClassifier.Classify(Grid(["MES", "VENTAS"], ["Enero", 10.0], ["Febrero", 11.0], ["Marzo", 12.0])).Type.ShouldBe(SheetType.MonthlySales);

    [Fact]
    public void Classify_TrialBalanceNeedsAccountCodes()
    {
        var withCodes = Grid(["BALANCE DE COMPROBACION"], ["CUENTA", "DESCRIPCION", "ACTIVO"], ["1101", "Caja", 1.0], ["1102", "Bancos", 2.0], ["1103", "CxC", 3.0]);
        var withoutCodes = Grid(["BALANCE DE COMPROBACION"], ["CUENTA", "DESCRIPCION", "ACTIVO"], ["Caja", "x", 1.0]);

        SheetClassifier.Classify(withCodes).Type.ShouldBe(SheetType.TrialBalance);
        SheetClassifier.Classify(withoutCodes).Type.ShouldBe(SheetType.Unknown);
    }

    [Fact]
    public void Classify_EmptySheet()
    {
        var result = SheetClassifier.Classify(Grid([null, null], []));

        result.Type.ShouldBe(SheetType.Unknown);
        result.Reason!.ShouldContain("vacía");
    }
}

public sealed class FinancialStatementExtractorTests
{
    [Fact]
    public void Extract_FindsYearsSectionsLabelsAndKinds()
    {
        var grid = new SheetGrid("BAL",
        [
            ["EMPRESA DEMO"],
            ["BALANCE GENERAL"],
            [null, null, null, "2024", null, 2023.0],
            ["ACTIVOS CORRIENTES"],
            [null, "Efectivo (nota 3)", null, 100.0, null, 90.0],
            [null, "Menos: Provisión", null, "(10)", null, null],
            [null, "Total corriente", null, 90.0, null, 90.0],
            [null, null, null, 90.0, null, 90.0],
            [null, "Total Activos", null, 90.0, null, 90.0],
        ]);

        var statement = FinancialStatementExtractor.Extract(grid, SheetType.BalanceSheet, 2026)!;
        var lines = statement.Lines.ToList();

        statement.Years.ShouldBe([2023, 2024]);
        statement.ColumnLetter(2024).ShouldBe("D");
        lines[0].NormalizedLabel.ShouldBe("Efectivo");
        lines[0].Section.ShouldBe("ACTIVOS CORRIENTES");
        lines[0].RowNumber.ShouldBe(5);
        lines[1].Values[2024].ShouldBe(-10m);
        lines[1].Values.ContainsKey(2023).ShouldBeFalse(); // celda vacía = sin dato, no cero
        lines[2].RowKind.ShouldBe(RowKind.Subtotal);
        lines[3].RowKind.ShouldBe(RowKind.Subtotal); // sin etiqueta pero con valores
        lines[4].RowKind.ShouldBe(RowKind.Total);
    }

    [Fact]
    public void FindYearRow_IgnoresYearsOutOfRangeAndRowsWithAmounts()
    {
        var grid = new SheetGrid("X",
        [
            [null, 1850.0, 1851.0],
            [null, 2024.0, 150000.0],
            [null, 2024.0, 2023.0],
        ]);

        FinancialStatementExtractor.FindYearRow(grid, 2026, out var years).ShouldBe(2);
        years.Keys.ShouldBe([2024, 2023], ignoreOrder: true);
    }

    [Theory]
    [InlineData("Total Activos", RowKind.Total)]
    [InlineData("Total Pasivos & Capital", RowKind.Total)]
    [InlineData("Total corriente", RowKind.Subtotal)]
    [InlineData("Total capital", RowKind.Subtotal)]
    [InlineData("Ventas netas", RowKind.Detail)]
    [InlineData("", RowKind.Subtotal)]
    public void Classify_RowKinds(string label, RowKind expected) => FinancialStatementExtractor.Classify(label).ShouldBe(expected);
}

public sealed class ToleranceAndCurrencyTests
{
    [Theory]
    [InlineData(1_000_000, 1_000_000.9, true)]
    [InlineData(100_000_000, 100_009_000, true)]
    [InlineData(100_000_000, 100_011_000, false)]
    [InlineData(500, 502, false)]
    public void Tolerance_IsOneUnitOrPointZeroOnePercent(double a, double b, bool equal) =>
        Tolerance.Equal((decimal)a, (decimal)b).ShouldBe(equal);

    [Theory]
    [InlineData("DOP", true)]
    [InlineData("USD", true)]
    [InlineData("EUR", true)]
    [InlineData("RD$", false)]
    [InlineData("usd", false)]
    [InlineData("XYZ", false)]
    public void Iso4217_ValidatesCurrencyCodes(string code, bool valid) => Iso4217.IsValid(code).ShouldBe(valid);
}
