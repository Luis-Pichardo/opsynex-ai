namespace Opsynex.Domain.Engines.Kpis;

public enum TrendDirection
{
    Up,
    Down,
    Stable,
}

/// <summary>Variación año contra año de un valor.</summary>
public sealed record Trend(
    decimal? Previous,
    decimal? Current,
    decimal? Change,
    decimal? ChangePercent,
    TrendDirection? Direction,
    bool? Favorable);

/// <summary>Tendencias de un año frente al anterior.</summary>
public sealed record YearTrends
{
    public int Year { get; init; }

    public int PreviousYear { get; init; }

    /// <summary>Tendencia de cada KPI.</summary>
    public IReadOnlyDictionary<KpiCode, Trend> Kpis { get; init; } = new Dictionary<KpiCode, Trend>();

    /// <summary>Crecimiento de ingresos, costo total y gastos generales (lo usa el diagnóstico).</summary>
    public Trend? RevenueGrowth { get; init; }

    public Trend? TotalCostGrowth { get; init; }

    public Trend? OverheadGrowth { get; init; }
}

/// <summary>Valores absolutos de un año que se comparan como crecimiento.</summary>
public sealed record GrowthInput(decimal? Revenue, decimal? TotalCost, decimal? OverheadCost);

/// <summary>
/// Tendencias año contra año: variación absoluta y porcentual, dirección (Up, Down o Stable con un
/// umbral de ±1 % por defecto) y si es favorable según el KPI (bajar la eficiencia de costos es
/// favorable; una variación estable no es ni favorable ni desfavorable).
/// </summary>
public static class TrendAnalyzer
{
    public const decimal DefaultStableThresholdPercent = 1m;

    public static Trend Compare(decimal? previous, decimal? current, bool higherIsBetter, decimal stableThresholdPercent = DefaultStableThresholdPercent)
    {
        if (previous is not { } p || current is not { } c)
        {
            return new Trend(previous, current, null, null, null, null);
        }

        var change = c - p;
        decimal? percent = p != 0 ? decimal.Round(change / Math.Abs(p) * 100m, 2) : null;
        var direction = percent switch
        {
            { } x when Math.Abs(x) <= stableThresholdPercent => TrendDirection.Stable,
            { } x => x > 0 ? TrendDirection.Up : TrendDirection.Down,
            null => change == 0 ? TrendDirection.Stable : change > 0 ? TrendDirection.Up : TrendDirection.Down,
        };
        bool? favorable = direction == TrendDirection.Stable ? null : (direction == TrendDirection.Up) == higherIsBetter;
        return new Trend(p, c, change, percent, direction, favorable);
    }

    public static YearTrends Analyze(
        int year,
        IReadOnlyDictionary<KpiCode, decimal?> current,
        GrowthInput currentGrowth,
        int previousYear,
        IReadOnlyDictionary<KpiCode, decimal?> previous,
        GrowthInput previousGrowth,
        decimal stableThresholdPercent = DefaultStableThresholdPercent)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(currentGrowth);
        ArgumentNullException.ThrowIfNull(previousGrowth);

        var kpis = KpiCatalog.All.ToDictionary(
            d => d.Code,
            d => Compare(previous.GetValueOrDefault(d.Code), current.GetValueOrDefault(d.Code), d.HigherIsBetter, stableThresholdPercent));

        return new YearTrends
        {
            Year = year,
            PreviousYear = previousYear,
            Kpis = kpis,
            RevenueGrowth = Compare(previousGrowth.Revenue, currentGrowth.Revenue, higherIsBetter: true, stableThresholdPercent),
            TotalCostGrowth = Compare(previousGrowth.TotalCost, currentGrowth.TotalCost, higherIsBetter: false, stableThresholdPercent),
            OverheadGrowth = Compare(previousGrowth.OverheadCost, currentGrowth.OverheadCost, higherIsBetter: false, stableThresholdPercent),
        };
    }
}
