using System.Globalization;
using System.Text.RegularExpressions;
using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;

namespace Opsynex.Infrastructure.Parsing;

/// <summary>
/// Validaciones de calidad de la sección 5 de FORMATO_EXCEL_ENTRADA.md. Todas producen
/// **advertencias** (con hoja, fila y columna), nunca bloquean la carga.
/// </summary>
public static partial class WorkbookQualityValidator
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-DO");

    public sealed record Input(
        IReadOnlyList<StatementSheet> Statements,
        SheetGrid? EquitySheet,
        TrialBalanceSheet? TrialBalance,
        IReadOnlyList<SheetGrid> AllSheets,
        int MaxYear);

    public sealed record Output(List<UploadIssue> Warnings, bool? TrialBalanceReliable);

    public static Output Validate(Input input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var warnings = new List<UploadIssue>();
        var balance = input.Statements.FirstOrDefault(s => s.SheetType == SheetType.BalanceSheet);
        var income = input.Statements.FirstOrDefault(s => s.SheetType == SheetType.IncomeStatement);

        if (balance is not null)
        {
            CheckBalance(balance, warnings);
        }

        CheckNotes(input.Statements, warnings);
        CheckEquity(input.EquitySheet, balance, income, input.MaxYear, warnings);
        var reliable = input.TrialBalance is null ? (bool?)null : CheckTrialBalance(input.TrialBalance, income, warnings);
        CheckCompanyName(input.AllSheets, warnings);
        foreach (var statement in input.Statements)
        {
            CheckSubtotals(statement, warnings);
        }

        CheckMissingYears(input.Statements, warnings);
        return new Output(warnings, reliable);
    }

    // 1. Total activos = total pasivos y capital.
    private static void CheckBalance(StatementSheet balance, List<UploadIssue> warnings)
    {
        var assets = balance.Lines.FirstOrDefault(l => TotalAssets().IsMatch(TextNormalizer.Key(l.NormalizedLabel)));
        var liabilitiesEquity = balance.Lines.FirstOrDefault(l => TotalLiabilitiesEquity().IsMatch(TextNormalizer.Key(l.NormalizedLabel)));
        if (assets is null || liabilitiesEquity is null)
        {
            return;
        }

        foreach (var year in balance.Years)
        {
            if (assets.Values.TryGetValue(year, out var a) && liabilitiesEquity.Values.TryGetValue(year, out var p)
                && Math.Abs(a - p) > Tolerance.For(a))
            {
                warnings.Add(new UploadIssue(
                    "quality.balance_mismatch",
                    $"{year}: el total de activos ({Money(a)}) no es igual al total de pasivos y capital ({Money(p)}); diferencia {Money(a - p)}.",
                    balance.SheetName, liabilitiesEquity.RowNumber, balance.ColumnLetter(year)));
            }
        }
    }

    // 2. Cada línea con "(nota N)" debe coincidir con el total de la nota N.
    private static void CheckNotes(IReadOnlyList<StatementSheet> statements, List<UploadIssue> warnings)
    {
        var notes = statements.Where(s => s.SheetType == SheetType.Notes).ToList();
        if (notes.Count == 0)
        {
            return;
        }

        foreach (var statement in statements.Where(s => s.SheetType is SheetType.BalanceSheet or SheetType.IncomeStatement))
        {
            foreach (var line in statement.Lines)
            {
                if (TextNormalizer.NoteNumber(line.OriginalLabel) is not { } number)
                {
                    continue;
                }

                foreach (var note in notes)
                {
                    var section = note.Entries.FirstOrDefault(e => e.IsSectionTitle && TextNormalizer.LeadingNoteNumber(e.Section) == number);
                    if (section is null)
                    {
                        continue;
                    }

                    var noteLines = note.Entries
                        .Where(e => e.Line is not null && e.Section == section.Section && e.RowNumber > section.RowNumber)
                        .Select(e => e.Line!)
                        .ToList();
                    var key = TextNormalizer.SingularKey(line.NormalizedLabel);
                    var total = noteLines.LastOrDefault(l => TextNormalizer.SingularKey(l.NormalizedLabel) == key)
                        ?? noteLines.LastOrDefault(l => l.RowKind != RowKind.Detail)
                        ?? noteLines.LastOrDefault();
                    if (total is null)
                    {
                        continue;
                    }

                    foreach (var year in statement.Years.Intersect(note.Years))
                    {
                        if (line.Values.TryGetValue(year, out var expected) && total.Values.TryGetValue(year, out var actual)
                            && !Tolerance.Equal(expected, actual))
                        {
                            warnings.Add(new UploadIssue(
                                "quality.note_mismatch",
                                $"{year}: la nota {number} suma {Money(actual)} y no cuadra con «{line.NormalizedLabel}» de la hoja {statement.SheetName} ({Money(expected)}).",
                                note.SheetName, total.RowNumber, note.ColumnLetter(year)));
                        }
                    }

                    break;
                }
            }
        }
    }

    // 3. El estado de cambios en el patrimonio debe reflejar la ganancia y el capital del balance.
    private static void CheckEquity(SheetGrid? equity, StatementSheet? balance, StatementSheet? income, int maxYear, List<UploadIssue> warnings)
    {
        if (equity is null)
        {
            return;
        }

        var year = (balance?.Years ?? income?.Years ?? []).DefaultIfEmpty(0).Max();
        if (year == 0)
        {
            return;
        }

        var netIncome = income?.Lines.LastOrDefault(l => NetIncome().IsMatch(TextNormalizer.Key(l.NormalizedLabel)))?.Values
            .GetValueOrDefault(year);
        var totalEquity = balance?.Lines.FirstOrDefault(l => TotalEquity().IsMatch(TextNormalizer.Key(l.NormalizedLabel)))?.Values
            .GetValueOrDefault(year);

        var profitRows = new List<(int Row, decimal Value)>();
        (int Row, decimal Value)? closing = null;
        for (var r = 0; r < equity.RowCount; r++)
        {
            var label = equity.TextCells(r).Select(c => c.Text).FirstOrDefault();
            if (label is null || RightmostNumber(equity, r) is not { } value)
            {
                continue;
            }

            var key = TextNormalizer.Key(label);
            var rowYear = SheetYearInText(label, maxYear);
            if (key.StartsWith("saldo", StringComparison.Ordinal) && rowYear == year)
            {
                closing = (r + 1, value);
            }
            else if (ProfitRow().IsMatch(key) && (rowYear is null || rowYear == year))
            {
                profitRows.Add((r + 1, value));
            }
        }

        if (netIncome is { } ni && ni != 0 && profitRows.Count > 0 && !profitRows.Any(p => Tolerance.Equal(p.Value, ni)))
        {
            warnings.Add(new UploadIssue(
                "quality.equity_mismatch",
                $"{year}: el estado de cambios en el patrimonio no refleja la ganancia del período ({Money(profitRows[^1].Value)} frente a {Money(ni)} del estado de resultados).",
                equity.Name, profitRows[^1].Row));
        }

        if (closing is { } c && totalEquity is { } te && te != 0 && !Tolerance.Equal(c.Value, te))
        {
            warnings.Add(new UploadIssue(
                "quality.equity_mismatch",
                $"{year}: el saldo final del patrimonio ({Money(c.Value)}) no coincide con el total de capital del balance ({Money(te)}).",
                equity.Name, c.Row));
        }
    }

    // 4. Balance de comprobación: prueba cero, saldos negativos y ventas.
    private static bool CheckTrialBalance(TrialBalanceSheet trial, StatementSheet? income, List<UploadIssue> warnings)
    {
        var reliable = true;
        var debit = trial.Leaves.Where(a => a.Nature is TrialBalanceNature.Asset or TrialBalanceNature.CostExpense).Sum(a => a.Value);
        if (Math.Abs(trial.ZeroTest) > Tolerance.For(debit))
        {
            reliable = false;
            warnings.Add(new UploadIssue(
                "quality.trial_balance_unbalanced",
                $"El balance de comprobación no cuadra (prueba cero = {Money(trial.ZeroTest)}); se usan los estados financieros como fuente principal.",
                trial.SheetName));
        }

        foreach (var account in trial.Leaves.Where(a => a.Nature == TrialBalanceNature.Asset && a.Value < 0
            && NegativeSensitive().IsMatch(TextNormalizer.Key(a.Description))))
        {
            reliable = false;
            warnings.Add(new UploadIssue(
                "quality.trial_balance_negative_balance",
                $"La cuenta {account.Code} «{account.Description}» tiene saldo negativo ({Money(account.Value)}) en el balance de comprobación.",
                trial.SheetName, account.RowNumber, SheetGrid.ColumnLetter(trial.Columns[account.Nature])));
        }

        var sales = income?.Lines.FirstOrDefault(l => GrossSales().IsMatch(TextNormalizer.Key(l.NormalizedLabel)));
        if (sales is not null && sales.Values.TryGetValue(trial.Year, out var statementSales))
        {
            var trialSales = trial.Leaves
                .Where(a => a.Nature == TrialBalanceNature.Income && a.Code.StartsWith('4')
                    && TextNormalizer.Key(a.Description).Contains("venta", StringComparison.Ordinal)
                    && !SalesAdjustment().IsMatch(TextNormalizer.Key(a.Description)))
                .Sum(a => a.Value);
            if (trialSales != 0 && !Tolerance.Equal(trialSales, statementSales))
            {
                reliable = false;
                warnings.Add(new UploadIssue(
                    "quality.trial_balance_sales_mismatch",
                    $"{trial.Year}: las ventas del balance de comprobación ({Money(trialSales)}) difieren de las del estado de resultados ({Money(statementSales)}).",
                    trial.SheetName));
            }
        }

        return reliable;
    }

    // 5. El nombre de la empresa (primera fila de cada hoja) debe ser el mismo en todas las hojas.
    //    El nombre no se guarda: el mensaje solo menciona las hojas.
    private static void CheckCompanyName(IReadOnlyList<SheetGrid> sheets, List<UploadIssue> warnings)
    {
        var names = sheets
            .Select(s => (Sheet: s, Name: s.RowCount > 0 ? s.TextCells(0).Select(c => c.Text).FirstOrDefault() : null))
            .Where(x => x.Name is not null)
            .Select(x => (x.Sheet, Key: TextNormalizer.Key(x.Name)))
            .Where(x => x.Key.Length > 0)
            .ToList();
        if (names.Count < 2)
        {
            return;
        }

        var reference = names.GroupBy(n => n.Key).OrderByDescending(g => g.Count()).First();
        var referenceSheet = reference.First().Sheet.Name;
        foreach (var (sheet, _) in names.Where(n => n.Key != reference.Key))
        {
            warnings.Add(new UploadIssue(
                "quality.company_name_mismatch",
                $"El nombre de la empresa en la hoja «{sheet.Name}» no coincide con el de la hoja «{referenceSheet}».",
                sheet.Name, 1, "A"));
        }
    }

    // Subtotales = suma de sus detalles (o subtotal anterior + detalles, o suma de subtotales de la sección).
    private static void CheckSubtotals(StatementSheet statement, List<UploadIssue> warnings)
    {
        foreach (var year in statement.Years)
        {
            string? section = null;
            var details = new List<decimal>();
            var sectionSubtotals = new List<decimal>();
            decimal? previousSubtotal = null;

            foreach (var entry in statement.Entries)
            {
                if (entry.Section != section)
                {
                    section = entry.Section;
                    details.Clear();
                    sectionSubtotals.Clear();
                    previousSubtotal = null;
                }

                if (entry.IsTitle)
                {
                    details.Clear();
                    continue;
                }

                var line = entry.Line!;
                if (!line.Values.TryGetValue(year, out var value))
                {
                    continue;
                }

                if (line.RowKind == RowKind.Detail)
                {
                    details.Add(TextNormalizer.IsSubtraction(line.OriginalLabel) && value > 0 ? -value : value);
                    continue;
                }

                if (line.RowKind == RowKind.Subtotal && (details.Count > 0 || sectionSubtotals.Count > 0))
                {
                    var sum = details.Sum();
                    var matches = (details.Count > 0 && Tolerance.Equal(sum, value))
                        || (details.Count > 0 && previousSubtotal is { } prev && Tolerance.Equal(prev + sum, value))
                        || (details.Count == 0 && Tolerance.Equal(sectionSubtotals.Sum(), value));
                    if (!matches)
                    {
                        var expected = details.Count > 0 ? sum : sectionSubtotals.Sum();
                        var label = line.NormalizedLabel.Length > 0 ? $"«{line.NormalizedLabel}»" : "El subtotal sin etiqueta";
                        warnings.Add(new UploadIssue(
                            "quality.subtotal_mismatch",
                            $"{year}: {label} ({Money(value)}) no coincide con la suma de sus líneas ({Money(expected)}).",
                            statement.SheetName, line.RowNumber, statement.ColumnLetter(year)));
                    }
                }

                if (line.RowKind == RowKind.Subtotal)
                {
                    sectionSubtotals.Add(value);
                    previousSubtotal = value;
                }

                details.Clear();
            }
        }
    }

    // El balance y el estado de resultados deben traer los mismos años.
    private static void CheckMissingYears(IReadOnlyList<StatementSheet> statements, List<UploadIssue> warnings)
    {
        var main = statements.Where(s => s.SheetType is SheetType.BalanceSheet or SheetType.IncomeStatement).ToList();
        var all = main.SelectMany(s => s.Years).Distinct().Order().ToList();
        foreach (var statement in main)
        {
            foreach (var year in all.Except(statement.Years))
            {
                warnings.Add(new UploadIssue(
                    "quality.missing_year",
                    $"La hoja «{statement.SheetName}» no trae el año {year}, que sí aparece en otros estados.",
                    statement.SheetName));
            }
        }
    }

    private static decimal? RightmostNumber(SheetGrid sheet, int row)
    {
        for (var c = sheet.ColumnCount(row) - 1; c >= 0; c--)
        {
            if (sheet.Cell(row, c) is not string && TextNormalizer.TryParseNumber(sheet.Cell(row, c), out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static int? SheetYearInText(string text, int maxYear)
    {
        var match = YearInText().Match(text);
        return match.Success && int.TryParse(match.Value, CultureInfo.InvariantCulture, out var year) && year <= maxYear ? year : null;
    }

    private static string Money(decimal value) => value.ToString("#,##0.00;(#,##0.00)", Es);

    [GeneratedRegex(@"^total (de )?(los )?activos?$", RegexOptions.CultureInvariant)]
    private static partial Regex TotalAssets();

    [GeneratedRegex(@"^total (de )?(los )?(pasivos? (y|e) (capital|patrimonio)|(capital|patrimonio) y pasivos?)$", RegexOptions.CultureInvariant)]
    private static partial Regex TotalLiabilitiesEquity();

    [GeneratedRegex(@"^total (de )?(del )?(capital|patrimonio)( contable| de los socios| de los accionistas)?$", RegexOptions.CultureInvariant)]
    private static partial Regex TotalEquity();

    [GeneratedRegex(@"^(ingresos netos|utilidad neta|ganancia neta|beneficio neto|perdida neta|resultado neto|utilidad del (ejercicio|periodo)|ganancia del (ejercicio|periodo)|beneficio del (ejercicio|periodo))$", RegexOptions.CultureInvariant)]
    private static partial Regex NetIncome();

    [GeneratedRegex(@"(beneficio|ganancia|utilidad|resultado|perdida)", RegexOptions.CultureInvariant)]
    private static partial Regex ProfitRow();

    [GeneratedRegex(@"^(ventas|ventas netas|ventas brutas|ingresos por ventas|ventas de bienes y servicios)$", RegexOptions.CultureInvariant)]
    private static partial Regex GrossSales();

    [GeneratedRegex(@"(descuento|devolucion|rebaja|bonificacion)", RegexOptions.CultureInvariant)]
    private static partial Regex SalesAdjustment();

    [GeneratedRegex(@"(banco|caja|efectivo|inventario)", RegexOptions.CultureInvariant)]
    private static partial Regex NegativeSensitive();

    [GeneratedRegex(@"\b(19|20)\d{2}\b", RegexOptions.CultureInvariant)]
    private static partial Regex YearInText();
}
