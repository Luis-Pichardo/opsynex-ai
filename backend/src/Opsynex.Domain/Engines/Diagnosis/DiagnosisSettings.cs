using Opsynex.Domain.Enums;

namespace Opsynex.Domain.Engines.Diagnosis;

/// <summary>
/// Umbrales de una regla. La regla calcula una <b>magnitud</b> (siempre positiva cuando hay
/// problema: puntos porcentuales de caída, diferencia de crecimiento, distancia al benchmark…):
/// &gt;= <see cref="High"/> → High; &gt;= <see cref="Medium"/> → Medium; &gt;= <see cref="Low"/> (y &gt; 0) → Low.
/// </summary>
public sealed class RuleSettings
{
    public bool Enabled { get; set; } = true;

    public decimal Low { get; set; }

    public decimal Medium { get; set; }

    public decimal High { get; set; }

    /// <summary>Valor de referencia por industria (o "Default"), para las reglas contra un benchmark.</summary>
    public Dictionary<string, decimal> Reference { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public RuleSettings()
    {
    }

    public RuleSettings(decimal low, decimal medium, decimal high, Dictionary<string, decimal>? reference = null)
    {
        Low = low;
        Medium = medium;
        High = high;
        if (reference is not null)
        {
            Reference = new Dictionary<string, decimal>(reference, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>Severidad para una magnitud; <c>null</c> si no llega al umbral.</summary>
    public Severity? Classify(decimal magnitude)
    {
        if (magnitude <= 0 || magnitude < Low)
        {
            return null;
        }

        return magnitude >= High ? Severity.High : magnitude >= Medium ? Severity.Medium : Severity.Low;
    }

    /// <summary>Referencia de la industria, o la "Default".</summary>
    public decimal? ReferenceFor(Industry industry) =>
        Reference.TryGetValue(industry.ToString(), out var value) ? value
        : Reference.TryGetValue("Default", out var fallback) ? fallback
        : null;
}

/// <summary>
/// Sección <c>DiagnosisRules</c>: umbrales de cada regla por código. Lo que no esté configurado usa
/// los valores por defecto de <see cref="Defaults"/>.
/// </summary>
public sealed class DiagnosisSettings
{
    public const string SectionName = "DiagnosisRules";

    public Dictionary<string, RuleSettings> Rules { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Umbrales por defecto (unidades en la documentación de cada regla).</summary>
    public static readonly IReadOnlyDictionary<string, RuleSettings> Defaults = new Dictionary<string, RuleSettings>(StringComparer.OrdinalIgnoreCase)
    {
        // Caída del margen en puntos porcentuales.
        ["EBITDA_DECLINE"] = new(0.5m, 2m, 5m),
        ["GROSS_MARGIN_DECLINE"] = new(0.5m, 2m, 5m),
        // Crecimiento del costo total (%) con ingresos estables (±Reference %).
        ["COST_GROWTH_FLAT_REVENUE"] = new(2m, 5m, 10m, new() { ["Default"] = 2m }),
        // Diferencia de crecimiento (pp) entre el costo y los ingresos.
        ["OVERHEAD_OUTPACES_REVENUE"] = new(0.01m, 5m, 10m),
        ["LABOR_COST_OUTPACES_REVENUE"] = new(0.01m, 5m, 10m),
        // Caída porcentual.
        ["PRODUCTIVITY_DECLINE"] = new(2m, 7m, 15m),
        ["REVENUE_DECLINE"] = new(2m, 7m, 15m),
        // Aumento relativo (%) del ratio de energía.
        ["ENERGY_COST_SPIKE"] = new(10m, 15m, 25m),
        // Puntos porcentuales por debajo / por encima del benchmark de la industria.
        ["LOW_EBITDA_MARGIN"] = new(0.01m, 2m, 5m, new() { ["Manufacturing"] = 0.10m, ["Services"] = 0.12m }),
        ["HIGH_OPEX_RATIO"] = new(0.01m, 5m, 10m, new() { ["Manufacturing"] = 0.25m, ["Services"] = 0.40m }),
        // Pérdida de materiales / COGS: pp por encima de la referencia (3 %).
        ["HIGH_MATERIAL_LOSS"] = new(0.01m, 2m, 5m, new() { ["Default"] = 0.03m }),
        // Razón corriente por debajo de la referencia (1,0): diferencia en veces.
        ["LOW_LIQUIDITY"] = new(0.0001m, 0.1m, 0.3m, new() { ["Default"] = 1.0m }),
        // Pasivos / patrimonio por encima de la referencia (3,0): diferencia en veces.
        ["HIGH_LEVERAGE"] = new(0.0001m, 1m, 2m, new() { ["Default"] = 3.0m }),
    };

    public RuleSettings For(string code) =>
        Rules.TryGetValue(code, out var configured) ? configured
        : Defaults.TryGetValue(code, out var fallback) ? fallback
        : new RuleSettings(0.0001m, decimal.MaxValue, decimal.MaxValue);
}
