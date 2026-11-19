using Opsynex.Domain.Enums;

namespace Opsynex.Infrastructure.Parsing;

/// <summary>
/// Clasifica una hoja por su contenido (títulos y encabezados de las primeras 15 filas), no por
/// su nombre, según la sección 2 de FORMATO_EXCEL_ENTRADA.md.
/// </summary>
public static class SheetClassifier
{
    private const int RowsToInspect = 15;

    public static readonly string[] MonthNames =
    [
        "enero", "febrero", "marzo", "abril", "mayo", "junio",
        "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre",
    ];

    private static readonly (SheetType Type, string[] Keywords)[] TitleRules =
    [
        (SheetType.Notes, ["notas a los estados financieros"]),
        (SheetType.EquityStatement, ["cambios en el patrimonio", "cambios en el capital", "estado de patrimonio"]),
        (SheetType.BalanceSheet, ["balance general", "balances generales", "estado de situacion"]),
        (SheetType.IncomeStatement, ["estado de ganancias", "estados de ganancias", "estado de resultados", "estados de resultados", "ganancias y perdidas"]),
        (SheetType.CashFlow, ["flujo de efectivo", "flujos de efectivo"]),
        (SheetType.CostStructure, ["estructura de costos"]),
        (SheetType.Operational, ["datos operacionales"]),
    ];

    public sealed record Classification(SheetType Type, string? Reason);

    public static Classification Classify(SheetGrid sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        if (sheet.IsEmpty)
        {
            return new Classification(SheetType.Unknown, "La hoja está vacía.");
        }

        var rows = Math.Min(RowsToInspect, sheet.RowCount);
        var cellKeys = new List<(int Row, int Col, string Key)>();
        for (var r = 0; r < rows; r++)
        {
            foreach (var (col, text) in sheet.TextCells(r))
            {
                cellKeys.Add((r, col, TextNormalizer.Key(text)));
            }
        }

        var allText = " " + string.Join(" | ", cellKeys.Select(c => c.Key)) + " ";

        if (allText.Contains("balance de comprobacion", StringComparison.Ordinal) && HasAccountCodeColumn(sheet))
        {
            return new Classification(SheetType.TrialBalance, null);
        }

        foreach (var (type, keywords) in TitleRules)
        {
            if (keywords.Any(k => allText.Contains(k, StringComparison.Ordinal)))
            {
                return new Classification(type, null);
            }
        }

        if (IsEmployeeRoster(cellKeys))
        {
            return new Classification(SheetType.EmployeeRoster, null);
        }

        if (IsMonthlySales(sheet, cellKeys))
        {
            return new Classification(SheetType.MonthlySales, null);
        }

        return new Classification(SheetType.Unknown, "No se reconoce el contenido (hoja fiscal o de trabajo); se ignora.");
    }

    /// <summary>Encabezados "codigo", "nombre" y "cedula", más "cargo", "sueldo" o los meses.</summary>
    private static bool IsEmployeeRoster(List<(int Row, int Col, string Key)> cells) =>
        cells.GroupBy(c => c.Row).Any(row =>
        {
            var keys = row.Select(c => c.Key).ToHashSet();
            return keys.Contains("codigo") && keys.Contains("nombre") && keys.Any(k => k.StartsWith("cedula", StringComparison.Ordinal))
                && (keys.Contains("cargo") || keys.Any(k => k.Contains("sueldo", StringComparison.Ordinal)) || keys.Contains("enero"));
        });

    /// <summary>Columna "mes" con meses debajo y una columna de ventas o ingresos.</summary>
    private static bool IsMonthlySales(SheetGrid sheet, List<(int Row, int Col, string Key)> cells)
    {
        foreach (var header in cells.Where(c => c.Key == "mes"))
        {
            var hasSales = cells.Any(c => c.Row == header.Row
                && (c.Key.Contains("ventas", StringComparison.Ordinal) || c.Key.Contains("ingresos", StringComparison.Ordinal)));
            var months = 0;
            for (var r = header.Row + 1; r < Math.Min(sheet.RowCount, header.Row + 20); r++)
            {
                var key = TextNormalizer.Key(sheet.Text(r, header.Col));
                if (MonthNames.Any(m => key.StartsWith(m, StringComparison.Ordinal)))
                {
                    months++;
                }
            }

            if (hasSales && months >= 3)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Encabezado "cuenta" con al menos 3 códigos numéricos debajo.</summary>
    private static bool HasAccountCodeColumn(SheetGrid sheet)
    {
        for (var r = 0; r < Math.Min(RowsToInspect + 5, sheet.RowCount); r++)
        {
            foreach (var (col, text) in sheet.TextCells(r))
            {
                if (TextNormalizer.Key(text) is not ("cuenta" or "codigo" or "cuentas"))
                {
                    continue;
                }

                var codes = 0;
                for (var below = r + 1; below < Math.Min(sheet.RowCount, r + 30); below++)
                {
                    if (TrialBalanceExtractor.NormalizeCode(sheet.Cell(below, col)) is not null)
                    {
                        codes++;
                    }
                }

                if (codes >= 3)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
