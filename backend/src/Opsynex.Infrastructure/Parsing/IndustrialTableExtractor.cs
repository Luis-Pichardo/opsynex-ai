using System.Globalization;
using Opsynex.Application.Uploads;
using Opsynex.Domain.Common;
using Opsynex.Domain.Engines.Industrial;
using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;
using static Opsynex.Application.Uploads.IndustrialTemplates;

namespace Opsynex.Infrastructure.Parsing;

/// <summary>
/// Lee las tablas del módulo industrial (producción, costos y estándares): busca la fila de
/// encabezados en las primeras filas de cada hoja, valida cada fila y descarta las inválidas con
/// una advertencia que indica la hoja, la fila y la columna. Un libro puede traer las tres tablas
/// en hojas distintas. Solo se leen las columnas de la plantilla.
/// </summary>
public static class IndustrialTableExtractor
{
    /// <summary>Filas en las que se busca el encabezado.</summary>
    public const int HeaderSearchRows = 15;

    /// <summary>Tope de advertencias por fila inválida (luego se resumen).</summary>
    public const int MaxRowWarnings = 30;

    private static readonly string[] DateFormats = ["yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy"];

    /// <summary>Hoja reconocida como tabla industrial.</summary>
    public sealed record DetectedTable(SheetGrid Sheet, Template Template, int HeaderRow, IReadOnlyDictionary<string, int> Columns);

    /// <summary>
    /// Si el archivo es industrial (por el tipo indicado o por sus encabezados), lo lee en
    /// <paramref name="result"/> y devuelve <c>true</c>. Si no lo es, devuelve <c>false</c> y no toca nada.
    /// </summary>
    public static bool TryParse(IReadOnlyList<SheetGrid> sheets, UploadFileType? hint, ParsedWorkbook result, int maxYear, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        ArgumentNullException.ThrowIfNull(result);
        var forced = hint is { } h && IsIndustrial(h) ? Find(h) : null;
        var detected = sheets.Where(s => !s.IsEmpty).Select(s => Detect(s, forced)).Where(d => d is not null).Select(d => d!).ToList();

        if (detected.Count == 0)
        {
            if (forced is null)
            {
                return false;
            }

            result.FileType = forced.FileType;
            result.Errors.Add(new UploadIssue(
                "upload.industrial_columns_missing",
                $"No se encontraron los encabezados de la plantilla de {Describe(forced.FileType)}. Columnas obligatorias: " +
                $"{string.Join(", ", forced.RequiredColumns.Select(c => c.Header))}{(forced.FileType == UploadFileType.Costs ? " y al menos un costo" : string.Empty)}. Descarga la plantilla."));
            return true;
        }

        var tables = new IndustrialTables();
        var rowWarnings = 0;
        foreach (var sheet in sheets)
        {
            var table = detected.FirstOrDefault(d => ReferenceEquals(d.Sheet, sheet));
            if (table is null)
            {
                result.Sheets.Add(new UploadSheet(sheet.Name, SheetType.Unknown, 0, Ignored: true,
                    sheet.IsEmpty ? "Hoja vacía." : "No tiene los encabezados de una plantilla industrial."));
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var before = tables.Count;
            var warnings = new List<UploadIssue>();
            Read(table, tables, warnings, maxYear);
            rowWarnings += warnings.Count;
            result.Warnings.AddRange(warnings.Take(Math.Max(0, MaxRowWarnings - (rowWarnings - warnings.Count))));
            result.Sheets.Add(new UploadSheet(sheet.Name, table.Template.SheetType, tables.Count - before, Ignored: false));
        }

        if (rowWarnings > MaxRowWarnings)
        {
            result.Warnings.Add(new UploadIssue(
                "upload.industrial_more_rows_skipped",
                $"Se omitieron {rowWarnings} filas con errores en total; solo se muestran las primeras {MaxRowWarnings}."));
        }

        result.FileType = detected.Select(d => d.Template.FileType).Order().First();
        result.Industrial = tables;
        result.Years.AddRange(tables.Dates.Select(d => d.Year).Distinct().Order());
        if (tables.Count == 0)
        {
            result.Errors.Add(new UploadIssue(
                "upload.industrial_no_rows",
                "El archivo tiene los encabezados de la plantilla, pero ninguna fila válida. Revisa las advertencias."));
        }

        return true;
    }

    /// <summary>Busca la fila de encabezados de una plantilla (o de cualquiera si no se fuerza una).</summary>
    public static DetectedTable? Detect(SheetGrid sheet, Template? forced = null)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        DetectedTable? best = null;
        for (var row = 0; row < Math.Min(HeaderSearchRows, sheet.RowCount); row++)
        {
            var headers = sheet.TextCells(row).Select(c => (c.Col, Key: TextKey.Normalize(c.Text))).ToList();
            if (headers.Count < 2)
            {
                continue;
            }

            foreach (var template in forced is null ? All : [forced])
            {
                var columns = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var column in template.Columns)
                {
                    var match = headers.FirstOrDefault(h => column.Accepted.Contains(h.Key));
                    if (match.Key is not null)
                    {
                        columns[column.Key] = match.Col;
                    }
                }

                var complete = template.RequiredColumns.All(c => columns.ContainsKey(c.Key))
                    && (template.FileType != UploadFileType.Costs || CostColumns.Any(columns.ContainsKey));
                if (complete && (best is null || columns.Count > best.Columns.Count))
                {
                    best = new DetectedTable(sheet, template, row, columns);
                }
            }

            if (best is not null)
            {
                return best;
            }
        }

        return null;
    }

    private static void Read(DetectedTable table, IndustrialTables tables, List<UploadIssue> warnings, int maxYear)
    {
        var sheet = table.Sheet;
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var row = table.HeaderRow + 1; row < sheet.RowCount; row++)
        {
            if (table.Columns.Values.All(c => SheetGrid.IsBlank(sheet.Cell(row, c))))
            {
                continue;
            }

            var reader = new RowReader(table, row, maxYear);
            switch (table.Template.FileType)
            {
                case UploadFileType.Production:
                    if (reader.Production() is { } production)
                    {
                        AddUnique(tables.Production, production, $"{production.Date:yyyy-MM-dd}|{production.MachineId}", seen, reader, warnings);
                    }

                    break;
                case UploadFileType.Costs:
                    if (reader.Cost() is { } cost)
                    {
                        AddUnique(tables.Costs, cost, $"{cost.Date:yyyy-MM-dd}|{cost.MachineId}", seen, reader, warnings);
                    }

                    break;
                default:
                    if (reader.Standard() is { } standard)
                    {
                        AddUnique(tables.Standards, standard, standard.MachineId, seen, reader, warnings);
                    }

                    break;
            }

            if (reader.Issue is { } issue)
            {
                warnings.Add(issue);
            }
        }
    }

    /// <summary>Si la clave se repite, gana la última fila (con una advertencia).</summary>
    private static void AddUnique<T>(List<T> list, T item, string key, Dictionary<string, int> seen, RowReader reader, List<UploadIssue> warnings)
    {
        if (seen.TryGetValue(key, out var index))
        {
            list[index] = item;
            warnings.Add(reader.Warning("upload.industrial_duplicate", $"Registro repetido ({key.Replace('|', ' ')}): se usa la última fila."));
            return;
        }

        seen[key] = list.Count;
        list.Add(item);
    }

    private static string Describe(UploadFileType type) => type switch
    {
        UploadFileType.Production => "producción",
        UploadFileType.Costs => "costos",
        _ => "estándares",
    };

    /// <summary>Lee y valida una fila; deja el primer problema en <see cref="Issue"/>.</summary>
    private sealed class RowReader(DetectedTable table, int row, int maxYear)
    {
        public UploadIssue? Issue { get; private set; }

        private int ExcelRow => row + 1;

        public UploadIssue Warning(string code, string message, string? column = null) =>
            new(code, $"Fila {ExcelRow}: {message} La fila se omitió.", table.Sheet.Name, ExcelRow, column);

        public ProductionRecord? Production()
        {
            var date = ReadDate(Date);
            var machine = ReadCode(Machine, required: true);
            var line = ReadCode(Line, required: false);
            var planned = ReadNumber(PlannedMinutes, required: true);
            var downtime = ReadNumber(DowntimeMinutes, required: true);
            var total = ReadNumber(TotalUnits, required: true);
            var good = ReadNumber(GoodUnits, required: true);
            var produced = ReadNumber(UnitsProduced, required: false);
            var cycle = ReadNumber(CycleTime, required: false);
            if (Issue is not null)
            {
                return null;
            }

            if (planned <= 0m)
            {
                return Fail<ProductionRecord>(PlannedMinutes, "los minutos planificados deben ser mayores que 0.");
            }

            if (downtime > planned)
            {
                return Fail<ProductionRecord>(DowntimeMinutes, "los minutos de parada no pueden superar los planificados.");
            }

            if (good > total)
            {
                return Fail<ProductionRecord>(GoodUnits, "las unidades buenas no pueden superar las unidades totales.");
            }

            return new ProductionRecord(date!.Value, machine!, line, produced ?? good!.Value, downtime!.Value, planned!.Value, cycle, good!.Value, total!.Value);
        }

        public CostRecord? Cost()
        {
            var date = ReadDate(Date);
            var machine = ReadCode(Machine, required: true);
            var labor = ReadNumber(LaborCost, required: false);
            var material = ReadNumber(MaterialCost, required: false);
            var energy = ReadNumber(EnergyCost, required: false);
            var overhead = ReadNumber(OverheadCost, required: false);
            if (Issue is not null)
            {
                return null;
            }

            if (labor is null && material is null && energy is null && overhead is null)
            {
                return Fail<CostRecord>(null, "no tiene ningún costo.");
            }

            return new CostRecord(date!.Value, machine!, labor ?? 0m, material ?? 0m, energy ?? 0m, overhead ?? 0m);
        }

        public StandardRecord? Standard()
        {
            var machine = ReadCode(Machine, required: true);
            var process = ReadText(ProcessName);
            var cycle = ReadNumber(StandardCycleTime, required: true);
            var cost = ReadNumber(StandardCost, required: false);
            var perMinute = ReadNumber(CostPerMinute, required: false);
            if (Issue is not null)
            {
                return null;
            }

            if (cycle <= 0m)
            {
                return Fail<StandardRecord>(StandardCycleTime, "el ciclo estándar debe ser mayor que 0.");
            }

            return new StandardRecord(machine!, cycle!.Value, cost, perMinute, process);
        }

        private T? Fail<T>(string? key, string message)
            where T : class
        {
            Issue ??= Warning("upload.industrial_invalid_row", message, key is null ? null : Letter(key));
            return null;
        }

        private object? Cell(string key) => table.Columns.TryGetValue(key, out var col) ? table.Sheet.Cell(row, col) : null;

        private string? Letter(string key) => table.Columns.TryGetValue(key, out var col) ? SheetGrid.ColumnLetter(col) : null;

        private string Header(string key) => table.Template.Columns.First(c => c.Key == key).Header;

        private DateOnly? ReadDate(string key)
        {
            if (Issue is not null)
            {
                return null;
            }

            var cell = Cell(key);
            DateOnly? date = cell switch
            {
                DateTime dt => DateOnly.FromDateTime(dt),
                double d when d is > 1 and < 2958466 => DateOnly.FromDateTime(DateTime.FromOADate(d)),
                string s when DateOnly.TryParseExact(s.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) => parsed,
                string s when DateTime.TryParse(s.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTime) => DateOnly.FromDateTime(parsedTime),
                _ => null,
            };
            if (date is null)
            {
                Fail<object>(key, $"la columna «{Header(key)}» no tiene una fecha válida.");
                return null;
            }

            if (date.Value.Year < 1900 || date.Value.Year > maxYear)
            {
                Fail<object>(key, $"la fecha {date:yyyy-MM-dd} está fuera de rango (1900–{maxYear}).");
                return null;
            }

            return date;
        }

        private string? ReadCode(string key, bool required)
        {
            if (Issue is not null)
            {
                return null;
            }

            var text = ReadText(key);
            if (text is null)
            {
                if (required)
                {
                    Fail<object>(key, $"falta «{Header(key)}».");
                }

                return null;
            }

            if (text.Length > 50)
            {
                Fail<object>(key, $"«{Header(key)}» tiene más de 50 caracteres.");
                return null;
            }

            return text.ToUpperInvariant();
        }

        private string? ReadText(string key)
        {
            var cell = Cell(key);
            var text = cell switch
            {
                null => null,
                string s => s.Trim(),
                double d => d.ToString(CultureInfo.InvariantCulture),
                _ => Convert.ToString(cell, CultureInfo.InvariantCulture)?.Trim(),
            };
            return string.IsNullOrWhiteSpace(text) ? null : text.Length > 200 ? text[..200] : text;
        }

        private decimal? ReadNumber(string key, bool required)
        {
            if (Issue is not null)
            {
                return null;
            }

            var cell = Cell(key);
            if (SheetGrid.IsBlank(cell))
            {
                if (required)
                {
                    Fail<object>(key, $"falta «{Header(key)}».");
                }

                return null;
            }

            if (!TextNormalizer.TryParseNumber(cell, out var value))
            {
                Fail<object>(key, $"«{Header(key)}» no es un número.");
                return null;
            }

            if (value < 0m)
            {
                Fail<object>(key, $"«{Header(key)}» no puede ser negativo.");
                return null;
            }

            return value;
        }
    }
}
