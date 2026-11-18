namespace Opsynex.Domain.Engines.Kpis;

/// <summary>
/// Rango de referencia: el valor <see cref="Worst"/> vale 0 puntos y <see cref="Best"/> vale 100.
/// Para los KPIs donde bajar es mejor, <c>Worst</c> es mayor que <c>Best</c>.
/// </summary>
public sealed record BenchmarkRange(decimal Worst, decimal Best);

/// <summary>Normaliza los KPIs a una escala de 0 a 100 con interpolación lineal y límites (lo usa el score).</summary>
public static class KpiNormalizer
{
    public static decimal? Normalize(decimal? value, BenchmarkRange? range)
    {
        if (value is not { } v || range is null || range.Worst == range.Best)
        {
            return null;
        }

        var score = (v - range.Worst) / (range.Best - range.Worst) * 100m;
        return decimal.Round(Math.Clamp(score, 0m, 100m), 2);
    }

    public static IReadOnlyDictionary<KpiCode, decimal?> Normalize(
        IReadOnlyDictionary<KpiCode, decimal?> values,
        IReadOnlyDictionary<KpiCode, BenchmarkRange> benchmarks)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(benchmarks);
        return values.ToDictionary(v => v.Key, v => Normalize(v.Value, benchmarks.GetValueOrDefault(v.Key)));
    }
}
