using Opsynex.Domain.Enums;

namespace Opsynex.Domain.Engines.Industrial;

/// <summary>
/// KPIs industriales (PLATAFORMA_DE_ANALISIS §5 y ESPECIFICACIONES §10). Clase pura: sin EF ni HTTP.
/// <list type="bullet">
/// <item>Availability = (planificado − downtime) / planificado.</item>
/// <item>Performance = (ciclo estándar × unidades totales) / tiempo en marcha.</item>
/// <item>Quality = unidades buenas / unidades totales.</item>
/// <item>OEE = Availability × Performance × Quality.</item>
/// <item>Scrap rate = (unidades totales − buenas) / unidades totales.</item>
/// <item>Cost per unit = costo total / unidades producidas.</item>
/// <item>Downtime ratio = downtime / planificado.</item>
/// <item>Capacity utilization = (ciclo estándar × unidades totales) / tiempo planificado.</item>
/// </list>
/// Toda división se protege: si el denominador es 0 o falta un dato, el KPI es <c>null</c>.
/// Los agregados de línea y planta se calculan con las sumas (no promediando ratios).
/// </summary>
public static class IndustrialKpiEngine
{
    private const int RatioDecimals = 4;
    private const int MoneyDecimals = 2;

    // --- Fórmulas ---

    public static decimal? Availability(decimal plannedMinutes, decimal downtimeMinutes) =>
        plannedMinutes > 0m ? Ratio(Math.Max(0m, plannedMinutes - downtimeMinutes) / plannedMinutes) : null;

    /// <param name="standardCycleSeconds">Ciclo estándar (segundos por unidad).</param>
    /// <param name="totalUnits">Unidades procesadas.</param>
    /// <param name="runMinutes">Tiempo en marcha (planificado − paradas), en minutos.</param>
    public static decimal? Performance(decimal? standardCycleSeconds, decimal totalUnits, decimal runMinutes) =>
        standardCycleSeconds is > 0m && runMinutes > 0m ? Ratio(IdealMinutes(standardCycleSeconds.Value, totalUnits) / runMinutes) : null;

    public static decimal? Quality(decimal goodUnits, decimal totalUnits) =>
        totalUnits > 0m ? Ratio(goodUnits / totalUnits) : null;

    public static decimal? Oee(decimal? availability, decimal? performance, decimal? quality) =>
        availability is { } a && performance is { } p && quality is { } q ? Ratio(a * p * q) : null;

    public static decimal? ScrapRate(decimal totalUnits, decimal goodUnits) =>
        totalUnits > 0m ? Ratio(Math.Max(0m, totalUnits - goodUnits) / totalUnits) : null;

    public static decimal? CostPerUnit(decimal? totalCost, decimal unitsProduced) =>
        totalCost is { } cost && unitsProduced > 0m ? Money(cost / unitsProduced) : null;

    public static decimal? DowntimeRatio(decimal downtimeMinutes, decimal plannedMinutes) =>
        plannedMinutes > 0m ? Ratio(downtimeMinutes / plannedMinutes) : null;

    public static decimal? CapacityUtilization(decimal? standardCycleSeconds, decimal totalUnits, decimal plannedMinutes) =>
        standardCycleSeconds is > 0m && plannedMinutes > 0m ? Ratio(IdealMinutes(standardCycleSeconds.Value, totalUnits) / plannedMinutes) : null;

    /// <summary>Minutos que habría tomado producir las unidades al ciclo estándar.</summary>
    public static decimal IdealMinutes(decimal standardCycleSeconds, decimal totalUnits) => standardCycleSeconds * totalUnits / 60m;

    // --- Pérdidas valoradas (PLATAFORMA_DE_ANALISIS §7) ---

    /// <summary>downtime_cost = downtime_minutes × cost_per_minute.</summary>
    public static decimal? DowntimeCost(decimal downtimeMinutes, decimal? costPerMinute) =>
        costPerMinute is { } cpm ? Money(Math.Max(0m, downtimeMinutes) * cpm) : null;

    /// <summary>scrap_cost = (total_units − good_units) × unit_cost.</summary>
    public static decimal? ScrapCost(decimal totalUnits, decimal goodUnits, decimal? unitCost) =>
        unitCost is { } cost ? Money(Math.Max(0m, totalUnits - goodUnits) * cost) : null;

    /// <summary>(tiempo en marcha − tiempo ideal) × costo por minuto: lo que cuesta ir más lento que el estándar.</summary>
    public static decimal? SpeedLossCost(decimal runMinutes, decimal? idealMinutes, decimal? costPerMinute) =>
        idealMinutes is { } ideal && costPerMinute is { } cpm ? Money(Math.Max(0m, runMinutes - ideal) * cpm) : null;

    // --- Agregación ---

    /// <summary>
    /// Agregados por año y por mes, cada uno para la planta, cada línea y cada máquina.
    /// El costo por minuto de una máquina es el de su estándar o, si falta, su costo real del
    /// período / minutos planificados; el costo unitario del scrap es el costo estándar o, si
    /// falta, el costo real / unidades procesadas.
    /// </summary>
    public static IReadOnlyList<IndustrialAggregate> Aggregate(
        IReadOnlyCollection<ProductionRecord> production,
        IReadOnlyCollection<CostRecord> costs,
        IReadOnlyCollection<StandardRecord> standards)
    {
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(costs);
        ArgumentNullException.ThrowIfNull(standards);
        var standardByMachine = StandardsByMachine(standards);
        var lineByMachine = LinesByMachine(production);

        var periods = production
            .SelectMany(p => new[] { (p.Date.Year, (int?)p.Date.Month), (p.Date.Year, (int?)null) })
            .Distinct()
            .OrderBy(p => p.Year)
            .ThenBy(p => p.Item2 ?? 0);

        var result = new List<IndustrialAggregate>();
        foreach (var (year, month) in periods)
        {
            var rows = production.Where(p => InPeriod(p.Date, year, month)).ToList();
            var periodCosts = costs.Where(c => InPeriod(c.Date, year, month)).ToList();

            var machines = rows
                .GroupBy(r => r.MachineId, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => Machine(year, month, g.Key, lineByMachine.GetValueOrDefault(g.Key), g.ToList(),
                    periodCosts.Where(c => string.Equals(c.MachineId, g.Key, StringComparison.OrdinalIgnoreCase)).ToList(),
                    standardByMachine.GetValueOrDefault(g.Key)))
                .ToList();

            result.Add(Combine(year, month, IndustrialLevel.Total, null, machines, rows));
            foreach (var line in machines.Where(m => m.LineId is not null).GroupBy(m => m.LineId!, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                var lineRows = rows.Where(r => line.Any(m => string.Equals(m.MachineId, r.MachineId, StringComparison.OrdinalIgnoreCase))).ToList();
                result.Add(Combine(year, month, IndustrialLevel.Line, line.Key, line.ToList(), lineRows));
            }

            result.AddRange(machines);
        }

        return result;
    }

    /// <summary>Serie diaria (OEE, disponibilidad, rendimiento, calidad y minutos de parada) del conjunto de registros.</summary>
    public static IReadOnlyList<IndustrialDailyPoint> Daily(IReadOnlyCollection<ProductionRecord> production, IReadOnlyCollection<StandardRecord> standards)
    {
        ArgumentNullException.ThrowIfNull(production);
        var standardByMachine = StandardsByMachine(standards);
        return production
            .GroupBy(p => p.Date)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var sums = ProductionSums(g.ToList(), standardByMachine);
                var kpis = Kpis(sums);
                return new IndustrialDailyPoint(g.Key, kpis.Oee, sums.DowntimeMinutes, kpis.Availability, kpis.Performance, kpis.Quality);
            })
            .ToList();
    }

    /// <summary>KPIs a partir de las sumas de un agregado.</summary>
    public static IndustrialKpis Kpis(IndustrialSums sums)
    {
        ArgumentNullException.ThrowIfNull(sums);
        var availability = Availability(sums.PlannedMinutes, sums.DowntimeMinutes);
        decimal? performance = sums.IdealMinutes is { } ideal && sums.RunMinutesWithStandard > 0m ? Ratio(ideal / sums.RunMinutesWithStandard) : null;
        var quality = Quality(sums.GoodUnits, sums.TotalUnits);
        return new IndustrialKpis(
            availability,
            performance,
            quality,
            Oee(availability, performance, quality),
            ScrapRate(sums.TotalUnits, sums.GoodUnits),
            DowntimeRatio(sums.DowntimeMinutes, sums.PlannedMinutes),
            sums.IdealMinutes is { } i && sums.PlannedMinutesWithStandard > 0m ? Ratio(i / sums.PlannedMinutesWithStandard) : null,
            CostPerUnit(sums.Costs?.Total, sums.UnitsWithCost),
            sums.StandardCostAmount is { } amount && sums.UnitsWithStandardCost > 0m ? Money(amount / sums.UnitsWithStandardCost) : null);
    }

    private static IndustrialAggregate Machine(
        int year,
        int? month,
        string machineId,
        string? lineId,
        List<ProductionRecord> rows,
        List<CostRecord> costs,
        StandardRecord? standard)
    {
        var standards = new Dictionary<string, StandardRecord>(StringComparer.OrdinalIgnoreCase);
        if (standard is not null)
        {
            standards[machineId] = standard;
        }

        var sums = ProductionSums(rows, standards);
        if (costs.Count > 0)
        {
            sums = sums with
            {
                Costs = costs.Aggregate(CostBreakdown.Zero, (acc, c) => acc.Add(new CostBreakdown(c.LaborCost, c.MaterialCost, c.EnergyCost, c.OverheadCost))),
                UnitsWithCost = sums.UnitsProduced,
            };
        }

        var actualCost = sums.Costs?.Total;
        var costPerMinute = standard?.CostPerMinute
            ?? (actualCost is { } c1 && sums.PlannedMinutes > 0m ? Money(c1 / sums.PlannedMinutes) : null);
        var unitCost = standard?.StandardCost
            ?? (actualCost is { } c2 && sums.TotalUnits > 0m ? Money(c2 / sums.TotalUnits) : null);
        var losses = new IndustrialLosses(
            DowntimeCost(sums.DowntimeMinutes, costPerMinute),
            ScrapCost(sums.TotalUnits, sums.GoodUnits, unitCost),
            SpeedLossCost(sums.RunMinutes, sums.IdealMinutes, costPerMinute));
        return new IndustrialAggregate(year, month, IndustrialLevel.Machine, machineId, lineId, sums, Kpis(sums), losses, costPerMinute, unitCost);
    }

    private static IndustrialAggregate Combine(int year, int? month, IndustrialLevel level, string? lineId, List<IndustrialAggregate> machines, List<ProductionRecord> rows)
    {
        var withStandard = machines.Where(m => m.Sums.IdealMinutes is not null).ToList();
        var withCost = machines.Where(m => m.Sums.Costs is not null).ToList();
        var withStandardCost = machines.Where(m => m.Sums.StandardCostAmount is not null).ToList();
        var sums = new IndustrialSums(
            machines.Sum(m => m.Sums.PlannedMinutes),
            machines.Sum(m => m.Sums.DowntimeMinutes),
            machines.Sum(m => m.Sums.TotalUnits),
            machines.Sum(m => m.Sums.GoodUnits),
            machines.Sum(m => m.Sums.UnitsProduced),
            rows.Select(r => r.Date).Distinct().Count(),
            withStandard.Count == 0 ? null : withStandard.Sum(m => m.Sums.IdealMinutes!.Value),
            withStandard.Sum(m => m.Sums.RunMinutesWithStandard),
            withStandard.Sum(m => m.Sums.PlannedMinutesWithStandard),
            withCost.Count == 0 ? null : withCost.Aggregate(CostBreakdown.Zero, (acc, m) => acc.Add(m.Sums.Costs!)),
            withCost.Sum(m => m.Sums.UnitsWithCost),
            withStandardCost.Count == 0 ? null : withStandardCost.Sum(m => m.Sums.StandardCostAmount!.Value),
            withStandardCost.Sum(m => m.Sums.UnitsWithStandardCost));
        var losses = machines.Aggregate(IndustrialLosses.None, (acc, m) => acc.Add(m.Losses));
        return new IndustrialAggregate(year, month, level, null, lineId, sums, Kpis(sums), losses);
    }

    private static IndustrialSums ProductionSums(List<ProductionRecord> rows, Dictionary<string, StandardRecord> standards)
    {
        decimal? ideal = null;
        decimal runWithStandard = 0m, plannedWithStandard = 0m, unitsWithStandardCost = 0m;
        decimal? standardCostAmount = null;
        foreach (var row in rows)
        {
            if (!standards.TryGetValue(row.MachineId, out var standard))
            {
                continue;
            }

            if (standard.StandardCycleTime > 0m)
            {
                ideal = (ideal ?? 0m) + IdealMinutes(standard.StandardCycleTime, row.TotalUnits);
                runWithStandard += Math.Max(0m, row.PlannedMinutes - row.DowntimeMinutes);
                plannedWithStandard += row.PlannedMinutes;
            }

            if (standard.StandardCost is { } cost)
            {
                standardCostAmount = (standardCostAmount ?? 0m) + (cost * row.UnitsProduced);
                unitsWithStandardCost += row.UnitsProduced;
            }
        }

        return new IndustrialSums(
            rows.Sum(r => r.PlannedMinutes),
            rows.Sum(r => r.DowntimeMinutes),
            rows.Sum(r => r.TotalUnits),
            rows.Sum(r => r.GoodUnits),
            rows.Sum(r => r.UnitsProduced),
            rows.Select(r => r.Date).Distinct().Count(),
            ideal,
            runWithStandard,
            plannedWithStandard,
            null,
            0m,
            standardCostAmount,
            unitsWithStandardCost);
    }

    private static Dictionary<string, StandardRecord> StandardsByMachine(IEnumerable<StandardRecord> standards) =>
        standards
            .GroupBy(s => s.MachineId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Línea de cada máquina: la del registro más reciente que la indica.</summary>
    private static Dictionary<string, string?> LinesByMachine(IEnumerable<ProductionRecord> production) =>
        production
            .GroupBy(p => p.MachineId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Where(p => !string.IsNullOrWhiteSpace(p.LineId)).OrderBy(p => p.Date).LastOrDefault()?.LineId,
                StringComparer.OrdinalIgnoreCase);

    private static bool InPeriod(DateOnly date, int year, int? month) => date.Year == year && (month is null || date.Month == month);

    private static decimal Ratio(decimal value) => decimal.Round(value, RatioDecimals);

    private static decimal Money(decimal value) => decimal.Round(value, MoneyDecimals);
}
