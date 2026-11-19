using System.Text.RegularExpressions;
using Opsynex.Application.Uploads;
using Opsynex.Domain.Enums;

namespace Opsynex.Infrastructure.Parsing;

/// <summary>Naturaleza de una columna del balance de comprobación.</summary>
public enum TrialBalanceNature
{
    Asset,
    LiabilityEquity,
    Income,
    CostExpense,
}

/// <summary>Cuenta del balance de comprobación.</summary>
public sealed record TrialBalanceAccount(int RowNumber, string Code, string Description, TrialBalanceNature Nature, decimal Value, bool IsLeaf);

/// <summary>Balance de comprobación extraído.</summary>
public sealed record TrialBalanceSheet(string SheetName, int Year, IReadOnlyList<TrialBalanceAccount> Accounts, IReadOnlyDictionary<TrialBalanceNature, int> Columns)
{
    public IEnumerable<TrialBalanceAccount> Leaves => Accounts.Where(a => a.IsLeaf);

    /// <summary>"Prueba cero": (activo + costos y gastos) − (pasivo y capital + ingresos), con las cuentas de último nivel.</summary>
    public decimal ZeroTest =>
        Leaves.Where(a => a.Nature is TrialBalanceNature.Asset or TrialBalanceNature.CostExpense).Sum(a => a.Value)
        - Leaves.Where(a => a.Nature is TrialBalanceNature.LiabilityEquity or TrialBalanceNature.Income).Sum(a => a.Value);
}

/// <summary>
/// Lee el balance de comprobación: código de cuenta jerárquico (1 activo, 2 pasivo, 3 capital,
/// 4 ventas, 5 costos, 6 gastos), descripción y columnas por naturaleza.
/// </summary>
public static partial class TrialBalanceExtractor
{
    public static TrialBalanceSheet? Extract(SheetGrid sheet, int year)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        for (var r = 0; r < Math.Min(30, sheet.RowCount); r++)
        {
            var cells = sheet.TextCells(r).Select(c => (c.Col, Key: TextNormalizer.Key(c.Text))).ToList();
            var code = cells.FirstOrDefault(c => c.Key is "cuenta" or "codigo" or "cuentas");
            if (code.Key is null)
            {
                continue;
            }

            var description = cells.FirstOrDefault(c => c.Key.StartsWith("descripcion", StringComparison.Ordinal) || c.Key is "nombre" or "nombre de la cuenta");
            var columns = new Dictionary<TrialBalanceNature, int>();
            foreach (var (col, key) in cells)
            {
                var nature = key switch
                {
                    _ when key.Contains("pasivo", StringComparison.Ordinal) => TrialBalanceNature.LiabilityEquity,
                    _ when key.StartsWith("activo", StringComparison.Ordinal) => TrialBalanceNature.Asset,
                    _ when key.Contains("ingreso", StringComparison.Ordinal) => TrialBalanceNature.Income,
                    _ when key.Contains("costo", StringComparison.Ordinal) || key.Contains("gasto", StringComparison.Ordinal) => TrialBalanceNature.CostExpense,
                    _ => (TrialBalanceNature?)null,
                };
                if (nature is { } n && !columns.ContainsKey(n))
                {
                    columns[n] = col;
                }
            }

            if (columns.Count == 0)
            {
                continue;
            }

            var raw = new List<(int Row, string Code, string Description, TrialBalanceNature Nature, decimal Value)>();
            for (var row = r + 1; row < sheet.RowCount; row++)
            {
                if (NormalizeCode(sheet.Cell(row, code.Col)) is not { } accountCode)
                {
                    continue;
                }

                var text = description.Key is not null ? sheet.Text(row, description.Col) : null;
                foreach (var (nature, col) in columns)
                {
                    if (TextNormalizer.TryParseNumber(sheet.Cell(row, col), out var value) && value != 0)
                    {
                        raw.Add((row + 1, accountCode, SensitiveDataScrubber.Scrub(text ?? string.Empty), nature, value));
                    }
                }
            }

            var codes = raw.Select(a => a.Code).Distinct().ToList();
            var accounts = raw
                .Select(a => new TrialBalanceAccount(a.Row, a.Code, a.Description, a.Nature, a.Value,
                    IsLeaf: !codes.Any(other => other.Length > a.Code.Length && other.StartsWith(a.Code, StringComparison.Ordinal))))
                .ToList();
            return new TrialBalanceSheet(sheet.Name, year, accounts, columns);
        }

        return null;
    }

    /// <summary>Filas del balance de comprobación como líneas en bruto (para homologar por código).</summary>
    public static IEnumerable<ParsedLine> ToLines(TrialBalanceSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return sheet.Accounts.Select(a => new ParsedLine(
            sheet.SheetName,
            SheetType.TrialBalance,
            a.RowNumber,
            a.Nature.ToString(),
            a.Description,
            TextNormalizer.CleanLabel(a.Description),
            a.Code,
            a.IsLeaf ? RowKind.Detail : RowKind.Subtotal,
            new Dictionary<int, decimal> { [sheet.Year] = a.Value }));
    }

    /// <summary>Código de cuenta: dígitos con separadores opcionales ("1101", "1.1.01", "11-01") → solo dígitos.</summary>
    public static string? NormalizeCode(object? cell)
    {
        var text = cell switch
        {
            string s => s.Trim(),
            double d when d >= 0 && d == Math.Floor(d) && d < 1e12 => d.ToString("0", System.Globalization.CultureInfo.InvariantCulture),
            _ => null,
        };
        return text is not null && AccountCode().IsMatch(text) ? new string(text.Where(char.IsDigit).ToArray()) : null;
    }

    [GeneratedRegex(@"^\d{1,4}([\.\-]?\d{1,6}){0,5}$", RegexOptions.CultureInvariant)]
    private static partial Regex AccountCode();
}
