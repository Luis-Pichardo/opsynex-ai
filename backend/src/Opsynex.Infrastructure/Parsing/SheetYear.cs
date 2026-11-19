using System.Text.RegularExpressions;

namespace Opsynex.Infrastructure.Parsing;

/// <summary>Busca el año en los títulos de una hoja ("AÑO 2024", "AL 31 DE DICIEMBRE DE 2024").</summary>
public static partial class SheetYear
{
    public static int? FromTitle(SheetGrid sheet, int maxYear, int rows = 6)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        for (var r = 0; r < Math.Min(rows, sheet.RowCount); r++)
        {
            foreach (var (_, text) in sheet.TextCells(r))
            {
                foreach (Match match in Year().Matches(text))
                {
                    var year = int.Parse(match.Value, System.Globalization.CultureInfo.InvariantCulture);
                    if (year >= FinancialStatementExtractor.MinYear && year <= maxYear)
                    {
                        return year;
                    }
                }
            }
        }

        return null;
    }

    [GeneratedRegex(@"\b(19|20)\d{2}\b", RegexOptions.CultureInvariant)]
    private static partial Regex Year();
}
