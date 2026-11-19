using Opsynex.Application.Uploads;

namespace Opsynex.Infrastructure.Parsing;

/// <summary>Serie mensual de ventas y compras (hoja tipo "CUADRO": columna MES y los 12 meses).</summary>
public static class MonthlySalesExtractor
{
    public static List<MonthlyValue> Extract(SheetGrid sheet, int year)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var result = new List<MonthlyValue>();
        for (var r = 0; r < Math.Min(30, sheet.RowCount); r++)
        {
            var cells = sheet.TextCells(r).Select(c => (c.Col, Key: TextNormalizer.Key(c.Text))).ToList();
            var month = cells.FirstOrDefault(c => c.Key == "mes");
            if (month.Key is null)
            {
                continue;
            }

            int? salesCol = cells.Where(c => (c.Key.Contains("ventas", StringComparison.Ordinal) || c.Key.Contains("ingresos", StringComparison.Ordinal))
                && !c.Key.Contains("itbis", StringComparison.Ordinal)).Select(c => (int?)c.Col).FirstOrDefault();
            int? purchasesCol = cells.Where(c => c.Key.Contains("compras", StringComparison.Ordinal)
                && !c.Key.Contains("itbis", StringComparison.Ordinal)).Select(c => (int?)c.Col).FirstOrDefault();
            if (salesCol is null)
            {
                continue;
            }

            for (var row = r + 1; row < sheet.RowCount; row++)
            {
                var key = TextNormalizer.Key(sheet.Text(row, month.Col));
                var index = Array.FindIndex(SheetClassifier.MonthNames, m => key.StartsWith(m, StringComparison.Ordinal)
                    || (key.Length >= 3 && m.StartsWith(key, StringComparison.Ordinal)));
                if (index < 0 || result.Any(m => m.Month == index + 1))
                {
                    continue;
                }

                decimal? sales = TextNormalizer.TryParseNumber(sheet.Cell(row, salesCol.Value), out var s) ? s : null;
                decimal? purchases = purchasesCol is { } pc && TextNormalizer.TryParseNumber(sheet.Cell(row, pc), out var p) ? p : null;
                if (sales is not null || purchases is not null)
                {
                    result.Add(new MonthlyValue(year, index + 1, sales, purchases));
                }
            }

            break;
        }

        return result.OrderBy(m => m.Month).ToList();
    }
}
