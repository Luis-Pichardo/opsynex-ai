using Opsynex.Domain.Enums;

namespace Opsynex.Domain.Engines.Industrial;

/// <summary>Rango de normalización a 0–100: <see cref="Worst"/> da 0 y <see cref="Best"/> da 100.</summary>
public sealed class ScoreRange
{
    public ScoreRange()
    {
    }

    public ScoreRange(decimal worst, decimal best) => (Worst, Best) = (worst, best);

    public decimal Worst { get; set; }

    public decimal Best { get; set; }
}

/// <summary>
/// Sección <c>Industrial</c>: umbrales de las reglas industriales, severidades, pesos del score
/// industrial y rangos de normalización. Todo es configurable desde <c>appsettings</c>.
/// </summary>
public sealed class IndustrialSettings
{
    public const string SectionName = "Industrial";

    /// <summary>LOW_OEE: OEE por debajo de este valor (0,60 según la especificación).</summary>
    public decimal LowOeeThreshold { get; set; } = 0.60m;

    /// <summary>HIGH_SCRAP: scrap por encima de este valor (0,05).</summary>
    public decimal HighScrapThreshold { get; set; } = 0.05m;

    /// <summary>HIGH_DOWNTIME: paradas / tiempo planificado por encima de este valor (0,15).</summary>
    public decimal HighDowntimeThreshold { get; set; } = 0.15m;

    /// <summary>COST_PER_UNIT_ABOVE_STANDARD: costo por unidad más de este % por encima del estándar (0,10 = 10 %).</summary>
    public decimal CostAboveStandardTolerance { get; set; } = 0.10m;

    /// <summary>Severidad de cada regla (la especificación fija High, Medium y High para las tres primeras).</summary>
    public Dictionary<string, Severity> Severities { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        [IndustrialDiagnosisEngine.LowOee] = Severity.High,
        [IndustrialDiagnosisEngine.HighScrap] = Severity.Medium,
        [IndustrialDiagnosisEngine.HighDowntime] = Severity.High,
        [IndustrialDiagnosisEngine.CostAboveStandard] = Severity.Medium,
    };

    /// <summary>Pesos del score industrial: producción 0,30, costo 0,25, financiero 0,25 y estándar 0,20.</summary>
    public Dictionary<string, decimal> ScoreWeights { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        [nameof(IndustrialDimension.Production)] = 0.30m,
        [nameof(IndustrialDimension.Cost)] = 0.25m,
        [nameof(IndustrialDimension.Financial)] = 0.25m,
        [nameof(IndustrialDimension.Standard)] = 0.20m,
    };

    /// <summary>OEE: 40 % → 0 y 85 % (clase mundial) → 100.</summary>
    public ScoreRange OeeRange { get; set; } = new(0.40m, 0.85m);

    /// <summary>Costo por unidad / estándar: 1,30 → 0 y 1,00 (o menos) → 100.</summary>
    public ScoreRange CostRatioRange { get; set; } = new(1.30m, 1.00m);

    /// <summary>Rendimiento frente al ciclo estándar: 60 % → 0 y 100 % → 100.</summary>
    public ScoreRange PerformanceRange { get; set; } = new(0.60m, 1.00m);

    /// <summary>Scrap: 10 % → 0 y 2 % (o menos) → 100.</summary>
    public ScoreRange ScrapRange { get; set; } = new(0.10m, 0.02m);

    public Severity SeverityOf(string code) => Severities.TryGetValue(code, out var severity) ? severity : Severity.Medium;

    public decimal WeightOf(IndustrialDimension dimension) => ScoreWeights.TryGetValue(dimension.ToString(), out var weight) ? weight : 0m;

    /// <summary>Errores de configuración (vacío si es válida).</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (LowOeeThreshold is <= 0m or > 1m)
        {
            errors.Add("Industrial:LowOeeThreshold debe estar entre 0 y 1.");
        }

        if (HighScrapThreshold is <= 0m or > 1m)
        {
            errors.Add("Industrial:HighScrapThreshold debe estar entre 0 y 1.");
        }

        if (HighDowntimeThreshold is <= 0m or > 1m)
        {
            errors.Add("Industrial:HighDowntimeThreshold debe estar entre 0 y 1.");
        }

        if (CostAboveStandardTolerance < 0m)
        {
            errors.Add("Industrial:CostAboveStandardTolerance no puede ser negativo.");
        }

        var unknown = ScoreWeights.Keys.Where(k => !Enum.TryParse<IndustrialDimension>(k, ignoreCase: true, out _)).ToList();
        if (unknown.Count > 0)
        {
            errors.Add($"Industrial:ScoreWeights tiene dimensiones desconocidas: {string.Join(", ", unknown)}.");
        }

        if (ScoreWeights.Values.Any(w => w < 0m) || Math.Abs(ScoreWeights.Values.Sum() - 1m) > 0.0001m)
        {
            errors.Add("Industrial:ScoreWeights deben ser positivos y sumar 1.");
        }

        foreach (var (name, range) in new[] { ("OeeRange", OeeRange), ("CostRatioRange", CostRatioRange), ("PerformanceRange", PerformanceRange), ("ScrapRange", ScrapRange) })
        {
            if (range is null || range.Worst == range.Best)
            {
                errors.Add($"Industrial:{name} necesita Worst distinto de Best.");
            }
        }

        return errors;
    }
}

/// <summary>Dimensiones del score industrial.</summary>
public enum IndustrialDimension
{
    Production,
    Cost,
    Financial,
    Standard,
}
