using System.Text.RegularExpressions;
using Opsynex.Application.Uploads;
using Opsynex.Domain.Enums;

namespace Opsynex.Infrastructure.Parsing;

/// <summary>
/// Algoritmo de lectura de estados financieros (sección 3 de FORMATO_EXCEL_ENTRADA.md), igual para
/// el balance, el estado de resultados, las notas y las plantillas, sin posiciones fijas:
/// <list type="number">
/// <item>Fila de años: la primera de las primeras 20 filas con 1 a 4 celdas enteras entre 1990 y el año actual + 1.</item>
/// <item>Etiqueta: el texto más a la derecha antes de la primera columna de valores; el texto de la col. A sin valores es la sección.</item>
/// <item>Etiqueta normalizada: sin "(nota N)", "(N.N)", "Menos:" ni "más:".</item>
/// <item>Clase: título (sin valores), detalle o subtotal/total ("Total…" o sin etiqueta con valores).</item>
/// <item>Valores: números o texto numérico; una celda vacía es "sin dato", no cero.</item>
/// </list>
/// </summary>
public static partial class FinancialStatementExtractor
{
    public const int MinYear = 1990;
    private const int RowsToSearchForYears = 20;

    public static StatementSheet? Extract(SheetGrid sheet, SheetType type, int maxYear)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var yearRow = FindYearRow(sheet, maxYear, out var yearColumns);
        if (yearRow < 0)
        {
            return null;
        }

        var statement = new StatementSheet { SheetName = sheet.Name, SheetType = type, YearColumns = yearColumns };
        var firstValueCol = yearColumns.Values.Min();
        string? section = null;

        for (var r = yearRow + 1; r < sheet.RowCount; r++)
        {
            var labels = sheet.TextCells(r).Where(c => c.Col < firstValueCol).ToList();
            var values = new Dictionary<int, decimal>();
            foreach (var (year, col) in yearColumns)
            {
                if (TextNormalizer.TryParseNumber(sheet.Cell(r, col), out var value))
                {
                    values[year] = value;
                }
            }

            if (labels.Count == 0 && values.Count == 0)
            {
                continue;
            }

            var rowNumber = r + 1;
            if (values.Count == 0)
            {
                // Título: si está en la col. A abre una sección; en cualquier caso corta el grupo de subtotales.
                var isSection = labels[0].Col == 0;
                if (isSection)
                {
                    section = SensitiveDataScrubber.Scrub(labels[0].Text);
                }

                statement.Entries.Add(new StatementEntry(rowNumber, section, null, IsTitle: true, IsSectionTitle: isSection));
                continue;
            }

            var original = labels.Count > 0 ? SensitiveDataScrubber.Scrub(labels[^1].Text) : string.Empty;
            var normalized = TextNormalizer.CleanLabel(original);
            var kind = Classify(normalized);

            var line = new ParsedLine(sheet.Name, type, rowNumber, section, original, normalized, null, kind, values);
            statement.Entries.Add(new StatementEntry(rowNumber, section, line, IsTitle: false, IsSectionTitle: false));
        }

        return statement;
    }

    /// <summary>Subtotal ("Total…" o sin etiqueta) o gran total ("Total Activos", "Total Pasivos &amp; Capital").</summary>
    public static RowKind Classify(string normalizedLabel)
    {
        var key = TextNormalizer.Key(normalizedLabel);
        if (key.Length == 0)
        {
            return RowKind.Subtotal;
        }

        if (!key.StartsWith("total", StringComparison.Ordinal))
        {
            return RowKind.Detail;
        }

        return GrandTotal().IsMatch(key) ? RowKind.Total : RowKind.Subtotal;
    }

    /// <summary>
    /// Fila de años: 1 a 4 celdas enteras (número o texto) entre 1990 y <paramref name="maxYear"/>,
    /// sin otros números en la fila.
    /// </summary>
    public static int FindYearRow(SheetGrid sheet, int maxYear, out Dictionary<int, int> yearColumns)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        for (var r = 0; r < Math.Min(RowsToSearchForYears, sheet.RowCount); r++)
        {
            var years = new Dictionary<int, int>();
            var otherNumbers = false;
            for (var c = 0; c < sheet.ColumnCount(r); c++)
            {
                var cell = sheet.Cell(r, c);
                if (!TextNormalizer.TryParseNumber(cell, out var number) || (cell is string s && s.Trim() is "-" or "—" or "–"))
                {
                    continue;
                }

                if (number == decimal.Truncate(number) && number >= MinYear && number <= maxYear && !years.ContainsKey((int)number))
                {
                    years[(int)number] = c;
                }
                else
                {
                    otherNumbers = true;
                }
            }

            if (years.Count is >= 1 and <= 4 && !otherNumbers)
            {
                yearColumns = years;
                return r;
            }
        }

        yearColumns = [];
        return -1;
    }

    // "total activos", "total pasivos y capital", "total de pasivos y patrimonio", "total capital y pasivos"…
    [GeneratedRegex(@"^total (de )?(los )?(activos?|pasivos?( (y|e) (capital|patrimonio))?|(capital|patrimonio) y pasivos?|general)$", RegexOptions.CultureInvariant)]
    private static partial Regex GrandTotal();
}
