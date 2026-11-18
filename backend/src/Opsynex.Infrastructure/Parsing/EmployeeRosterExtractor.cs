using System.Text.RegularExpressions;
using Opsynex.Application.Uploads;
using Opsynex.Domain.Entities;

namespace Opsynex.Infrastructure.Parsing;

/// <summary>
/// Cuenta los empleados de la nómina (filas con código de empleado), en total y por departamento.
/// **Privacidad:** solo devuelve conteos y la suma de sueldos por departamento; nunca lee ni
/// devuelve nombres, cédulas, cargos ni sueldos individuales.
/// </summary>
public static partial class EmployeeRosterExtractor
{
    public const string NoDepartment = "Sin departamento";

    public static EmployeeSummary? Extract(SheetGrid sheet, int year)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var header = FindHeader(sheet);
        if (header is null)
        {
            return null;
        }

        var (headerRow, codeCol, monthCols, totalCol, salaryCol) = header.Value;
        var departments = new Dictionary<string, (int Count, decimal Payroll)>(StringComparer.Ordinal);
        var order = new List<string>();
        var current = NoDepartment;

        for (var r = headerRow + 1; r < sheet.RowCount; r++)
        {
            var code = sheet.Cell(r, codeCol);
            var codeText = code switch
            {
                string s => s.Trim(),
                double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
                _ => null,
            };

            if (string.IsNullOrEmpty(codeText))
            {
                continue;
            }

            if (!EmployeeCode().IsMatch(codeText))
            {
                // Fila de departamento ("DEPARTAMENTO: PRODUCCION"); los totales se ignoran.
                var key = TextNormalizer.Key(codeText);
                if (!key.StartsWith("total", StringComparison.Ordinal))
                {
                    current = DepartmentName(codeText);
                }

                continue;
            }

            var salary = monthCols.Count > 0
                ? monthCols.Sum(c => TextNormalizer.TryParseNumber(sheet.Cell(r, c), out var v) ? v : 0)
                : totalCol is { } t && TextNormalizer.TryParseNumber(sheet.Cell(r, t), out var total) ? total
                : salaryCol is { } s2 && TextNormalizer.TryParseNumber(sheet.Cell(r, s2), out var monthly) ? monthly * 12
                : 0;

            if (!departments.TryGetValue(current, out var info))
            {
                order.Add(current);
                info = (0, 0);
            }

            departments[current] = (info.Count + 1, info.Payroll + salary);
        }

        if (departments.Count == 0)
        {
            return null;
        }

        var byDepartment = order
            .Select(d => new DepartmentSummary(d, departments[d].Count, decimal.Round(departments[d].Payroll, 2)))
            .ToList();
        return new EmployeeSummary(year, byDepartment.Sum(d => d.Employees), byDepartment, byDepartment.Sum(d => d.Payroll));
    }

    private static (int Row, int CodeCol, List<int> MonthCols, int? TotalCol, int? SalaryCol)? FindHeader(SheetGrid sheet)
    {
        for (var r = 0; r < Math.Min(30, sheet.RowCount); r++)
        {
            var cells = sheet.TextCells(r).Select(c => (c.Col, Key: TextNormalizer.Key(c.Text))).ToList();
            var code = cells.FirstOrDefault(c => c.Key == "codigo");
            if (code.Key is null || !cells.Any(c => c.Key == "nombre"))
            {
                continue;
            }

            var months = cells.Where(c => SheetClassifier.MonthNames.Contains(c.Key)).Select(c => c.Col).ToList();
            int? total = cells.Where(c => c.Key == "total").Select(c => (int?)c.Col).FirstOrDefault();
            int? salary = cells.Where(c => c.Key.Contains("sueldo", StringComparison.Ordinal)).Select(c => (int?)c.Col).FirstOrDefault();
            return (r, code.Col, months, total, salary);
        }

        return null;
    }

    private static string DepartmentName(string text)
    {
        var name = DepartmentPrefix().Replace(text, string.Empty).Trim();
        name = name.Length == 0 ? text.Trim() : name;
        return System.Globalization.CultureInfo.GetCultureInfo("es").TextInfo.ToTitleCase(name.ToLowerInvariant());
    }

    // Códigos de empleado: "001", "15", "EMP-001", "E 12".
    [GeneratedRegex(@"^[A-Za-z]{0,4}[-\s]?\d{1,6}$", RegexOptions.CultureInvariant)]
    private static partial Regex EmployeeCode();

    [GeneratedRegex(@"^\s*(departamento|depto\.?|area|área)\s*[:\-]?\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DepartmentPrefix();
}
