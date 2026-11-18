namespace Opsynex.Domain.Engines.Kpis;

/// <summary>Ingresos de un mes.</summary>
public sealed record MonthlyRevenue(int Year, int Month, decimal? Revenue);

/// <summary>Indicadores de un mes de la serie.</summary>
public sealed record MonthlyPoint(
    int Year,
    int Month,
    decimal? Revenue,
    decimal? GrowthPercent,
    decimal? MovingAverage3,
    decimal? SeasonalityIndex);

/// <summary>
/// Serie mensual de ingresos: crecimiento contra el mes anterior, promedio móvil de 3 meses y un
/// índice de estacionalidad básico (ingreso del mes / promedio mensual de su año; 1 = mes promedio).
/// </summary>
public static class MonthlyAnalyzer
{
    public static IReadOnlyList<MonthlyPoint> Analyze(IEnumerable<MonthlyRevenue> months)
    {
        ArgumentNullException.ThrowIfNull(months);
        var ordered = months.OrderBy(m => m.Year).ThenBy(m => m.Month).ToList();
        var yearAverages = ordered
            .Where(m => m.Revenue is not null)
            .GroupBy(m => m.Year)
            .ToDictionary(g => g.Key, g => g.Average(m => m.Revenue!.Value));

        var result = new List<MonthlyPoint>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var month = ordered[i];
            var previous = i > 0 && IsConsecutive(ordered[i - 1], month) ? ordered[i - 1].Revenue : null;
            decimal? growth = month.Revenue is { } r && previous is { } p && p != 0 ? decimal.Round((r - p) / Math.Abs(p) * 100m, 2) : null;

            decimal? average = null;
            if (i >= 2 && IsConsecutive(ordered[i - 2], ordered[i - 1]) && IsConsecutive(ordered[i - 1], month)
                && ordered[i - 2].Revenue is { } a && ordered[i - 1].Revenue is { } b && month.Revenue is { } c)
            {
                average = decimal.Round((a + b + c) / 3m, 2);
            }

            decimal? seasonality = month.Revenue is { } value && yearAverages.TryGetValue(month.Year, out var avg) && avg > 0
                ? decimal.Round(value / avg, 4)
                : null;

            result.Add(new MonthlyPoint(month.Year, month.Month, month.Revenue, growth, average, seasonality));
        }

        return result;
    }

    private static bool IsConsecutive(MonthlyRevenue a, MonthlyRevenue b) =>
        (b.Year * 12 + b.Month) - (a.Year * 12 + a.Month) == 1;
}
