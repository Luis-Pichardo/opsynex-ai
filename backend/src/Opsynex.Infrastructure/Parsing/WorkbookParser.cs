using Microsoft.Extensions.Options;
using Opsynex.Application.Uploads;
using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;

namespace Opsynex.Infrastructure.Parsing;

/// <summary>
/// Lee el archivo, clasifica cada hoja por su contenido, extrae los estados financieros, la
/// nómina (solo conteos), las ventas mensuales y el balance de comprobación, y aplica las
/// validaciones de calidad. Los errores bloqueantes son solo: archivo ilegible o vacío, o que no
/// haya ni balance ni estado de resultados (salvo una plantilla simple de otro tipo).
/// </summary>
public sealed class WorkbookParser(IOptions<UploadOptions> options, TimeProvider timeProvider) : IWorkbookParser
{
    private static readonly SheetType[] StatementTypes =
    [
        SheetType.BalanceSheet, SheetType.IncomeStatement, SheetType.Notes,
        SheetType.CashFlow, SheetType.CostStructure, SheetType.Operational,
    ];

    private static readonly SheetType[] TemplateOnlyTypes = [SheetType.CashFlow, SheetType.CostStructure, SheetType.Operational];

    public ParsedWorkbook Parse(Stream content, string fileName, UploadFileType? fileTypeHint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);

        var result = new ParsedWorkbook();
        var read = new WorkbookReader(options.Value).Read(buffer.ToArray(), fileName);
        result.Errors.AddRange(read.Errors);
        result.Warnings.AddRange(read.Warnings);
        if (result.HasBlockingErrors)
        {
            return result;
        }

        if (read.Sheets.Count == 0 || read.Sheets.All(s => s.IsEmpty))
        {
            result.Errors.Add(new UploadIssue("upload.empty_file", "El archivo no tiene datos."));
            return result;
        }

        var maxYear = timeProvider.GetUtcNow().Year + 1;
        var hintType = fileTypeHint is { } hint ? UploadTemplates.SheetTypeFor(hint) : null;
        var nonEmpty = read.Sheets.Count(s => !s.IsEmpty);

        // 1. Clasificar.
        var classified = new List<(SheetGrid Sheet, SheetType Type, string? Reason)>();
        foreach (var sheet in read.Sheets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (type, reason) = SheetClassifier.Classify(sheet);
            if (type == SheetType.Unknown && hintType is { } forced && nonEmpty == 1 && !sheet.IsEmpty)
            {
                (type, reason) = (forced, null);
            }

            classified.Add((sheet, type, reason));
        }

        // 2. Extraer los estados (balance, resultados, notas y plantillas).
        var statements = new List<StatementSheet>();
        var reports = new Dictionary<string, UploadSheet>(StringComparer.Ordinal);
        foreach (var (sheet, type, reason) in classified.Where(c => StatementTypes.Contains(c.Type)))
        {
            var statement = FinancialStatementExtractor.Extract(sheet, type, maxYear);
            if (statement is null)
            {
                reports[sheet.Name] = new UploadSheet(sheet.Name, type, 0, Ignored: true, "No se encontró la fila de años (por ejemplo 2023 y 2024).");
                result.Warnings.Add(new UploadIssue(
                    "upload.years_not_found_in_sheet",
                    $"La hoja «{sheet.Name}» parece un {Describe(type)} pero no tiene una fila con los años.",
                    sheet.Name));
                continue;
            }

            statements.Add(statement);
            var lines = statement.Lines.ToList();
            result.Lines.AddRange(lines);
            reports[sheet.Name] = new UploadSheet(sheet.Name, type, lines.Count, Ignored: false, reason);
        }

        var mainYears = statements
            .Where(s => s.SheetType is SheetType.BalanceSheet or SheetType.IncomeStatement || TemplateOnlyTypes.Contains(s.SheetType))
            .SelectMany(s => s.Years)
            .Distinct()
            .Order()
            .ToList();
        result.Years.AddRange(mainYears);
        var currentYear = mainYears.Count > 0 ? mainYears[^1] : (int?)null;

        // 3. Hojas especiales.
        SheetGrid? equity = null;
        TrialBalanceSheet? trial = null;
        foreach (var (sheet, type, reason) in classified.Where(c => !StatementTypes.Contains(c.Type)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var year = currentYear ?? SheetYear.FromTitle(sheet, maxYear) ?? maxYear - 1;
            switch (type)
            {
                case SheetType.EmployeeRoster:
                    var titleYear = SheetYear.FromTitle(sheet, maxYear) ?? year;
                    result.Employees = EmployeeRosterExtractor.Extract(sheet, titleYear);
                    reports[sheet.Name] = new UploadSheet(sheet.Name, type, result.Employees?.Count ?? 0, Ignored: result.Employees is null,
                        result.Employees is null ? "No se encontraron filas con código de empleado." : "Solo se guarda la cantidad de empleados.");
                    break;
                case SheetType.MonthlySales:
                    var monthly = MonthlySalesExtractor.Extract(sheet, SheetYear.FromTitle(sheet, maxYear) ?? year);
                    result.Monthly.AddRange(monthly);
                    reports[sheet.Name] = new UploadSheet(sheet.Name, type, monthly.Count, Ignored: monthly.Count == 0,
                        monthly.Count == 0 ? "No se encontraron meses con ventas." : null);
                    break;
                case SheetType.TrialBalance:
                    trial = TrialBalanceExtractor.Extract(sheet, SheetYear.FromTitle(sheet, maxYear) ?? year);
                    var accounts = trial is null ? [] : TrialBalanceExtractor.ToLines(trial).ToList();
                    result.Lines.AddRange(accounts);
                    reports[sheet.Name] = new UploadSheet(sheet.Name, type, accounts.Count, Ignored: trial is null,
                        trial is null ? "No se encontraron las columnas del balance de comprobación." : null);
                    break;
                case SheetType.EquityStatement:
                    equity = sheet;
                    reports[sheet.Name] = new UploadSheet(sheet.Name, type, 0, Ignored: false, "Se usa para validar el patrimonio.");
                    break;
                default:
                    reports[sheet.Name] = new UploadSheet(sheet.Name, SheetType.Unknown, 0, Ignored: true, reason);
                    break;
            }
        }

        result.Sheets.AddRange(read.Sheets.Select(s => reports[s.Name]));

        // 4. Errores bloqueantes.
        var hasMain = statements.Any(s => s.SheetType is SheetType.BalanceSheet or SheetType.IncomeStatement);
        var templateStatement = statements.FirstOrDefault(s => TemplateOnlyTypes.Contains(s.SheetType));
        if (!hasMain && templateStatement is null)
        {
            if (classified.Any(c => c.Type is SheetType.BalanceSheet or SheetType.IncomeStatement))
            {
                result.Errors.Add(new UploadIssue(
                    "upload.years_not_found",
                    $"Se encontraron los estados financieros, pero no una fila con años válidos (entre {FinancialStatementExtractor.MinYear} y {maxYear})."));
            }
            else if (fileTypeHint is null && nonEmpty == 1 && read.Sheets.Any(s => FinancialStatementExtractor.FindYearRow(s, maxYear, out _) >= 0))
            {
                result.Errors.Add(new UploadIssue(
                    "upload.file_type_required",
                    "No se reconoce el tipo de archivo. Indica el tipo (fileType) o usa una de las plantillas."));
            }
            else
            {
                result.Errors.Add(new UploadIssue(
                    "upload.no_financial_statements",
                    "No se encontró ni el balance general ni el estado de resultados. Sube el libro contable completo o una plantilla."));
            }

            return result;
        }

        result.FileType = DetectFileType(statements, fileTypeHint);
        result.Operational.AddRange(OperationalMapper.Map(statements.Where(s => s.SheetType == SheetType.Operational)));
        if (result.Employees is { } employees && !result.Operational.Any(o => o.Year == employees.Year && o.Field == OperationalField.EmployeeCount))
        {
            result.Operational.Add(new OperationalValue(employees.Year, OperationalField.EmployeeCount, employees.Count));
        }

        // 5. Validaciones de calidad (advertencias).
        var quality = WorkbookQualityValidator.Validate(new WorkbookQualityValidator.Input(statements, equity, trial, read.Sheets, maxYear));
        result.Warnings.AddRange(quality.Warnings);
        if (trial is not null)
        {
            result.TrialBalance = new TrialBalanceResult(trial.Year, quality.TrialBalanceReliable ?? true, decimal.Round(trial.ZeroTest, 2), trial.Accounts.Count);
        }

        return result;
    }

    private static UploadFileType DetectFileType(List<StatementSheet> statements, UploadFileType? hint)
    {
        var types = statements.Select(s => s.SheetType).Distinct().ToList();
        if (types.Count == 1)
        {
            var template = UploadTemplates.All.FirstOrDefault(t => t.SheetType == types[0]);
            if (template is not null)
            {
                return template.FileType;
            }
        }

        return hint is { } h && types.Count <= 1 ? h : UploadFileType.AccountingWorkbook;
    }

    private static string Describe(SheetType type) => type switch
    {
        SheetType.BalanceSheet => "balance general",
        SheetType.IncomeStatement => "estado de resultados",
        SheetType.Notes => "hoja de notas",
        SheetType.CashFlow => "estado de flujo de efectivo",
        SheetType.CostStructure => "estructura de costos",
        SheetType.Operational => "hoja de datos operacionales",
        _ => "estado financiero",
    };
}
