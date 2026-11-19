using Opsynex.Domain.Engines.Kpis;
using Opsynex.Domain.Enums;

namespace Opsynex.Application.Analysis;

/// <summary>Rango de referencia de un KPI en la configuración: <c>Worst</c> = 0 pts, <c>Best</c> = 100 pts.</summary>
public sealed class BenchmarkRangeOptions
{
    public decimal Worst { get; set; }

    public decimal Best { get; set; }
}

/// <summary>
/// Sección <c>Benchmarks</c>: rangos de referencia por industria (KPIs en proporción, veces o días)
/// y por moneda (KPIs en monto, como ingresos por empleado), para normalizar a 0–100.
/// </summary>
public sealed class BenchmarkOptions
{
    public const string SectionName = "Benchmarks";

    /// <summary>Variación porcentual máxima para considerar un KPI estable (±1 % por defecto).</summary>
    public decimal StableThresholdPercent { get; set; } = TrendAnalyzer.DefaultStableThresholdPercent;

    /// <summary>Industria (<c>Manufacturing</c>, <c>Services</c>) → KPI → rango.</summary>
    public Dictionary<string, Dictionary<string, BenchmarkRangeOptions>> Industries { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Moneda ISO 4217 (<c>DOP</c>, <c>USD</c>…) → KPI en monto → rango.</summary>
    public Dictionary<string, Dictionary<string, BenchmarkRangeOptions>> Currencies { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Rangos aplicables a una empresa: los de su industria más los de su moneda.</summary>
    public IReadOnlyDictionary<KpiCode, BenchmarkRange> For(Industry industry, string currency)
    {
        var result = new Dictionary<KpiCode, BenchmarkRange>();
        Add(Industries, industry.ToString(), result, monetary: false);
        Add(Currencies, currency, result, monetary: true);
        return result;
    }

    /// <summary>Claves de KPI desconocidas en la configuración (para validar al arrancar).</summary>
    public IEnumerable<string> InvalidKeys() =>
        Industries.Values.Concat(Currencies.Values).SelectMany(d => d.Keys).Where(k => !Enum.TryParse<KpiCode>(k, true, out _));

    private static void Add(
        Dictionary<string, Dictionary<string, BenchmarkRangeOptions>> source, string key, Dictionary<KpiCode, BenchmarkRange> target, bool monetary)
    {
        if (!source.TryGetValue(key, out var ranges))
        {
            return;
        }

        foreach (var (name, range) in ranges)
        {
            if (Enum.TryParse<KpiCode>(name, ignoreCase: true, out var code)
                && (KpiCatalog.Get(code).Unit == KpiUnit.Money) == monetary)
            {
                target[code] = new BenchmarkRange(range.Worst, range.Best);
            }
        }
    }
}
