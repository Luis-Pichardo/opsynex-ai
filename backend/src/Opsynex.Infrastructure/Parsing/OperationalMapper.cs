using Opsynex.Application.Uploads;

namespace Opsynex.Infrastructure.Parsing;

/// <summary>Asigna las filas de la plantilla de datos operacionales a los campos de <c>OperationalData</c>.</summary>
public static class OperationalMapper
{
    public static IEnumerable<OperationalValue> Map(IEnumerable<StatementSheet> sheets)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        foreach (var line in sheets.SelectMany(s => s.Lines))
        {
            if (FieldFor(line.NormalizedLabel) is not { } field)
            {
                continue;
            }

            foreach (var (year, value) in line.Values)
            {
                yield return new OperationalValue(year, field, value);
            }
        }
    }

    public static OperationalField? FieldFor(string label)
    {
        var key = TextNormalizer.Key(label);
        return key switch
        {
            _ when key.Contains("unidades", StringComparison.Ordinal) || key.Contains("produccion anual", StringComparison.Ordinal) => OperationalField.UnitsProduced,
            _ when key.Contains("empleados", StringComparison.Ordinal) || key.Contains("trabajadores", StringComparison.Ordinal) => OperationalField.EmployeeCount,
            _ when key.Contains("kwh", StringComparison.Ordinal) || key.Contains("consumo energetico", StringComparison.Ordinal) => OperationalField.EnergyConsumptionKwh,
            _ when key.Contains("scrap", StringComparison.Ordinal) || key.Contains("desperdicio", StringComparison.Ordinal) || key.Contains("merma", StringComparison.Ordinal) => OperationalField.ScrapEstimate,
            _ when key.Contains("costos operacionales", StringComparison.Ordinal) || key.Contains("costo operacional", StringComparison.Ordinal) => OperationalField.OperationalCosts,
            _ when key.Contains("energia", StringComparison.Ordinal) => OperationalField.EnergyCost,
            _ => null,
        };
    }
}
