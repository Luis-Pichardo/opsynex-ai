using Opsynex.Domain.Engines.Scoring;
using Opsynex.Domain.Enums;

namespace Opsynex.Domain.Engines.Industrial;

/// <summary>Insumos del score industrial de un período.</summary>
/// <param name="Oee">OEE del período.</param>
/// <param name="Performance">Rendimiento frente al ciclo estándar.</param>
/// <param name="ScrapRate">Scrap del período.</param>
/// <param name="CostPerUnit">Costo real por unidad.</param>
/// <param name="StandardCostPerUnit">Costo estándar por unidad.</param>
/// <param name="FinancialScore">Score Opsynex (financiero) del año, si existe.</param>
public sealed record IndustrialScoreInput(
    decimal? Oee,
    decimal? Performance,
    decimal? ScrapRate,
    decimal? CostPerUnit,
    decimal? StandardCostPerUnit,
    decimal? FinancialScore);

/// <summary>Componente del score industrial.</summary>
public sealed record IndustrialScoreComponent(
    IndustrialDimension Dimension,
    decimal? Value,
    decimal BaseWeight,
    decimal Weight,
    IReadOnlyDictionary<string, decimal?> Inputs,
    string? MissingReason);

/// <summary>Score industrial (0–100) con sus componentes.</summary>
public sealed record IndustrialScoreResult(
    decimal? Total,
    ScoreInterpretation? Interpretation,
    IReadOnlyList<IndustrialScoreComponent> Components,
    bool WeightsRedistributed)
{
    public IndustrialScoreComponent Component(IndustrialDimension dimension) => Components.First(c => c.Dimension == dimension);
}

/// <summary>
/// Score industrial (PLATAFORMA_DE_ANALISIS §8):
/// <c>production_score × 0,30 + cost_score × 0,25 + financial_score × 0,25 + standard_score × 0,20</c>.
/// <list type="bullet">
/// <item><b>Producción</b>: OEE normalizado (40 % → 0, 85 % → 100).</item>
/// <item><b>Costo</b>: costo por unidad / estándar (1,30 → 0, 1,00 → 100).</item>
/// <item><b>Financiero</b>: el Score Opsynex del año.</item>
/// <item><b>Estándar</b>: promedio del rendimiento frente al ciclo estándar y del scrap frente a su objetivo.</item>
/// </list>
/// Si falta un componente, su peso se reparte proporcionalmente entre los demás (como en el Score Opsynex).
/// </summary>
public static class IndustrialScoreEngine
{
    public static IndustrialScoreResult Calculate(IndustrialScoreInput input, IndustrialSettings settings)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(settings);

        var costRatio = input.CostPerUnit is { } cpu && input.StandardCostPerUnit is { } std && std > 0m ? decimal.Round(cpu / std, 4) : (decimal?)null;
        var performance = Normalize(input.Performance, settings.PerformanceRange);
        var scrap = Normalize(input.ScrapRate, settings.ScrapRange);
        var raw = new (IndustrialDimension Dimension, decimal? Value, Dictionary<string, decimal?> Inputs, string Missing)[]
        {
            (IndustrialDimension.Production, Normalize(input.Oee, settings.OeeRange),
                new() { ["Oee"] = input.Oee },
                "Falta el OEE (sube la producción y los estándares de ciclo)."),
            (IndustrialDimension.Cost, Normalize(costRatio, settings.CostRatioRange),
                new() { ["CostPerUnit"] = input.CostPerUnit, ["StandardCostPerUnit"] = input.StandardCostPerUnit, ["CostRatio"] = costRatio },
                "Faltan los costos o el costo estándar por unidad."),
            (IndustrialDimension.Financial, input.FinancialScore is { } f ? Clamp(f) : null,
                new() { ["OpsynexScore"] = input.FinancialScore },
                "No hay Score Opsynex del año (sube los estados financieros)."),
            (IndustrialDimension.Standard, Average(performance, scrap),
                new() { ["Performance"] = input.Performance, ["ScrapRate"] = input.ScrapRate, ["PerformanceScore"] = performance, ["ScrapScore"] = scrap },
                "Faltan el rendimiento y el scrap del período."),
        };

        var available = raw.Where(r => r.Value is not null).Sum(r => settings.WeightOf(r.Dimension));
        var components = raw.Select(r =>
        {
            var baseWeight = settings.WeightOf(r.Dimension);
            var weight = r.Value is null || available == 0m ? 0m : decimal.Round(baseWeight / available, 4);
            return new IndustrialScoreComponent(r.Dimension, r.Value, baseWeight, weight, r.Inputs, r.Value is null ? r.Missing : null);
        }).ToList();

        if (available == 0m)
        {
            return new IndustrialScoreResult(null, null, components, false);
        }

        var total = decimal.Round(raw.Where(r => r.Value is not null).Sum(r => settings.WeightOf(r.Dimension) * r.Value!.Value) / available, 2);
        total = Clamp(total);
        return new IndustrialScoreResult(total, ScoreEngine.Interpret(total), components, available < 1m);
    }

    /// <summary>Normaliza linealmente a 0–100 (sirve también para rangos invertidos, donde menos es mejor).</summary>
    public static decimal? Normalize(decimal? value, ScoreRange range)
    {
        ArgumentNullException.ThrowIfNull(range);
        if (value is not { } v || range.Best == range.Worst)
        {
            return null;
        }

        return Clamp(decimal.Round((v - range.Worst) / (range.Best - range.Worst) * 100m, 2));
    }

    private static decimal? Average(params decimal?[] values)
    {
        var present = values.Where(v => v is not null).Select(v => v!.Value).ToList();
        return present.Count == 0 ? null : decimal.Round(present.Average(), 2);
    }

    private static decimal Clamp(decimal value) => Math.Clamp(value, 0m, 100m);
}
