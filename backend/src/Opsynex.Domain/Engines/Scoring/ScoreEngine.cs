using Opsynex.Domain.Engines.Kpis;
using Opsynex.Domain.Enums;

namespace Opsynex.Domain.Engines.Scoring;

/// <summary>Configuración del score (sección <c>Score</c> de <c>appsettings.json</c>).</summary>
public sealed class ScoreSettings
{
    public const string SectionName = "Score";

    /// <summary>Pesos por componente (sección 13): deben sumar 1.</summary>
    public Dictionary<string, decimal> Weights { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        [nameof(KpiDimension.Profitability)] = 0.30m,
        [nameof(KpiDimension.CostEfficiency)] = 0.25m,
        [nameof(KpiDimension.Productivity)] = 0.25m,
        [nameof(KpiDimension.FinancialStability)] = 0.20m,
    };

    /// <summary>Peso de la tendencia de ingresos por empleado dentro de Productividad (el resto es el nivel).</summary>
    public decimal ProductivityTrendWeight { get; set; } = 0.30m;

    /// <summary>Variación anual de ingresos por empleado (en %) que vale 100 puntos; su opuesto vale 0 y 0 % vale 50.</summary>
    public decimal ProductivityTrendRangePercent { get; set; } = 10m;

    /// <summary>Desviación estándar del margen EBITDA (en pp) que vale 0 puntos; 0 pp vale 100.</summary>
    public decimal MarginVolatilityWorstPp { get; set; } = 5m;

    /// <summary>Años (incluido el actual) para medir la volatilidad de márgenes.</summary>
    public int MarginVolatilityWindowYears { get; set; } = 3;

    public decimal WeightFor(KpiDimension dimension) => Weights.GetValueOrDefault(dimension.ToString());

    /// <summary>Errores de configuración (vacío si es válida).</summary>
    public IEnumerable<string> Validate()
    {
        foreach (var key in Weights.Keys.Where(k => !Enum.TryParse<KpiDimension>(k, true, out _)))
        {
            yield return $"Score:Weights tiene un componente desconocido: {key}.";
        }

        if (Weights.Values.Any(w => w < 0) || Math.Abs(Weights.Values.Sum() - 1m) > 0.001m)
        {
            yield return "Score:Weights deben ser positivos y sumar 1.";
        }

        if (ProductivityTrendWeight is < 0 or > 1 || ProductivityTrendRangePercent <= 0 || MarginVolatilityWorstPp <= 0
            || MarginVolatilityWindowYears < 2)
        {
            yield return "Score: parámetros de tendencia o volatilidad fuera de rango.";
        }
    }
}

/// <summary>Datos de un año para el score.</summary>
public sealed record ScoreInput
{
    /// <summary>KPIs del año normalizados a 0–100 (clave = <see cref="KpiCode"/>).</summary>
    public required IReadOnlyDictionary<KpiCode, decimal?> Normalized { get; init; }

    public decimal? RevenuePerEmployee { get; init; }

    public decimal? PreviousRevenuePerEmployee { get; init; }

    /// <summary>Márgenes EBITDA de los últimos años, del más antiguo al actual (para la volatilidad).</summary>
    public IReadOnlyList<decimal> EbitdaMarginHistory { get; init; } = [];
}

/// <summary>Componente del score con sus insumos (0–100 cada uno) y el peso que terminó usando.</summary>
/// <param name="Dimension">Componente.</param>
/// <param name="BaseWeight">Peso configurado (sección 13).</param>
/// <param name="Weight">Peso usado tras redistribuir; 0 si el componente falta.</param>
/// <param name="Value">Puntaje del componente (0–100) o <c>null</c> si no hay datos.</param>
/// <param name="Inputs">Puntaje de cada insumo; <c>null</c> los que no se pudieron calcular.</param>
/// <param name="MissingReason">Motivo cuando falta el componente.</param>
public sealed record ScoreComponent(
    KpiDimension Dimension,
    decimal BaseWeight,
    decimal Weight,
    decimal? Value,
    IReadOnlyDictionary<string, decimal?> Inputs,
    string? MissingReason);

/// <summary>Resultado del score de un año.</summary>
public sealed record ScoreResult(
    decimal? Total,
    ScoreInterpretation? Interpretation,
    IReadOnlyList<ScoreComponent> Components,
    bool WeightsRedistributed)
{
    public ScoreComponent Component(KpiDimension dimension) => Components.Single(c => c.Dimension == dimension);
}

/// <summary>
/// Score Industrial-Financiero Opsynex (sección 13): promedio ponderado de cuatro componentes de 0 a 100.
/// Cada componente promedia sus insumos disponibles (KPIs normalizados, tendencia y volatilidad). Si un
/// componente no tiene datos, su peso se redistribuye proporcionalmente entre los demás. Clase pura.
/// </summary>
public static class ScoreEngine
{
    public const string ProductivityTrend = "RevenuePerEmployeeTrend";

    public const string MarginVolatility = "EbitdaMarginVolatility";

    public static ScoreResult Calculate(ScoreInput input, ScoreSettings settings)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(settings);

        var raw = new List<(KpiDimension Dimension, decimal? Value, Dictionary<string, decimal?> Inputs, string? Missing)>
        {
            Average(KpiDimension.Profitability, input, [KpiCode.EbitdaMargin, KpiCode.GrossMargin], [],
                "Faltan el margen EBITDA y el margen bruto."),
            Average(KpiDimension.CostEfficiency, input, [KpiCode.CostEfficiency, KpiCode.OperatingCostRatio], [],
                "Faltan los costos o los gastos operacionales."),
            Productivity(input, settings),
            Average(KpiDimension.FinancialStability, input, [KpiCode.CurrentRatio, KpiCode.DebtToEquity],
                [(MarginVolatility, Volatility(input.EbitdaMarginHistory, settings))],
                "Faltan el balance y la serie de márgenes."),
        };

        var available = raw.Where(r => r.Value is not null).ToList();
        var availableWeight = available.Sum(r => settings.WeightFor(r.Dimension));
        var components = raw.Select(r =>
        {
            var baseWeight = settings.WeightFor(r.Dimension);
            var weight = r.Value is null || availableWeight == 0 ? 0m : decimal.Round(baseWeight / availableWeight, 4);
            return new ScoreComponent(r.Dimension, baseWeight, weight, r.Value, r.Inputs, r.Missing);
        }).ToList();

        if (availableWeight == 0)
        {
            return new ScoreResult(null, null, components, false);
        }

        var total = available.Sum(r => r.Value!.Value * settings.WeightFor(r.Dimension)) / availableWeight;
        total = decimal.Round(Math.Clamp(total, 0m, 100m), 2);
        var redistributed = available.Count < raw.Count;
        return new ScoreResult(total, Interpret(total), components, redistributed);
    }

    /// <summary>80–100 saludable · 60–79 oportunidades de mejora · menos de 60 deterioro significativo.</summary>
    public static ScoreInterpretation Interpret(decimal score) => score switch
    {
        >= 80m => ScoreInterpretation.Healthy,
        >= 60m => ScoreInterpretation.Improvable,
        _ => ScoreInterpretation.Deteriorated,
    };

    /// <summary>Texto de la interpretación (sección 13).</summary>
    public static string Label(ScoreInterpretation interpretation) => interpretation switch
    {
        ScoreInterpretation.Healthy => "Operación saludable",
        ScoreInterpretation.Improvable => "Oportunidades de mejora",
        _ => "Deterioro operacional significativo",
    };

    /// <summary>Tendencia de ingresos por empleado: −rango % → 0, 0 % → 50, +rango % → 100.</summary>
    public static decimal? TrendScore(decimal? previous, decimal? current, decimal rangePercent)
    {
        if (previous is not { } p || current is not { } c || p <= 0 || rangePercent <= 0)
        {
            return null;
        }

        var change = (c - p) / p * 100m;
        return decimal.Round(Math.Clamp(50m + (change / rangePercent * 50m), 0m, 100m), 2);
    }

    /// <summary>Volatilidad del margen EBITDA (desviación estándar poblacional en pp): 0 pp → 100, peor → 0.</summary>
    public static decimal? Volatility(IReadOnlyList<decimal> history, ScoreSettings settings)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(settings);
        var window = history.TakeLast(settings.MarginVolatilityWindowYears).Select(m => m * 100m).ToList();
        if (window.Count < 2)
        {
            return null;
        }

        var mean = window.Average();
        var variance = window.Sum(m => (m - mean) * (m - mean)) / window.Count;
        var stdDev = (decimal)Math.Sqrt((double)variance);
        return KpiNormalizer.Normalize(stdDev, new BenchmarkRange(settings.MarginVolatilityWorstPp, 0m));
    }

    private static (KpiDimension, decimal?, Dictionary<string, decimal?>, string?) Productivity(ScoreInput input, ScoreSettings settings)
    {
        var level = input.Normalized.GetValueOrDefault(KpiCode.RevenuePerEmployee);
        var trend = TrendScore(input.PreviousRevenuePerEmployee, input.RevenuePerEmployee, settings.ProductivityTrendRangePercent);
        var inputs = new Dictionary<string, decimal?> { [nameof(KpiCode.RevenuePerEmployee)] = level, [ProductivityTrend] = trend };
        decimal? value = (level, trend) switch
        {
            ({ } l, { } t) => decimal.Round((l * (1 - settings.ProductivityTrendWeight)) + (t * settings.ProductivityTrendWeight), 2),
            ({ } l, null) => l,
            (null, { } t) => t,
            _ => null,
        };
        return (KpiDimension.Productivity, value, inputs, value is null ? "Falta la cantidad de empleados (nómina o datos operacionales)." : null);
    }

    private static (KpiDimension, decimal?, Dictionary<string, decimal?>, string?) Average(
        KpiDimension dimension, ScoreInput input, KpiCode[] kpis, (string Name, decimal? Value)[] extra, string missing)
    {
        var inputs = kpis.ToDictionary(k => k.ToString(), k => input.Normalized.GetValueOrDefault(k));
        foreach (var (name, value) in extra)
        {
            inputs[name] = value;
        }

        var values = inputs.Values.Where(v => v is not null).Select(v => v!.Value).ToList();
        decimal? average = values.Count == 0 ? null : decimal.Round(values.Average(), 2);
        return (dimension, average, inputs, average is null ? missing : null);
    }
}
