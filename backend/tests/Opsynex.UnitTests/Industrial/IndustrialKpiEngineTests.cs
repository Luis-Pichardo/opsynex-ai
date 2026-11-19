using Opsynex.Domain.Engines.Industrial;
using Opsynex.Domain.Enums;

namespace Opsynex.UnitTests.Industrial;

/// <summary>Fórmulas industriales (PLATAFORMA_DE_ANALISIS §5 y §7) y su agregación.</summary>
public sealed class IndustrialKpiEngineTests
{
    // --- Fórmulas ---

    [Fact]
    public void Availability_IsPlannedMinusDowntimeOverPlanned()
    {
        IndustrialKpiEngine.Availability(960m, 60m).ShouldBe(0.9375m);
        IndustrialKpiEngine.Availability(960m, 0m).ShouldBe(1m);
        IndustrialKpiEngine.Availability(100m, 150m).ShouldBe(0m); // nunca negativa
        IndustrialKpiEngine.Availability(0m, 0m).ShouldBeNull();   // sin tiempo planificado
    }

    [Fact]
    public void Performance_IsStandardCycleTimesUnitsOverRunTime()
    {
        // 30 s × 1.500 unidades = 750 min ideales en 900 min de marcha.
        IndustrialKpiEngine.Performance(30m, 1500m, 900m).ShouldBe(0.8333m);
        IndustrialKpiEngine.Performance(null, 1500m, 900m).ShouldBeNull();
        IndustrialKpiEngine.Performance(0m, 1500m, 900m).ShouldBeNull();
        IndustrialKpiEngine.Performance(30m, 1500m, 0m).ShouldBeNull();
        IndustrialKpiEngine.IdealMinutes(30m, 1500m).ShouldBe(750m);
    }

    [Fact]
    public void Quality_IsGoodOverTotal()
    {
        IndustrialKpiEngine.Quality(950m, 1000m).ShouldBe(0.95m);
        IndustrialKpiEngine.Quality(0m, 0m).ShouldBeNull();
    }

    [Fact]
    public void Oee_IsAvailabilityTimesPerformanceTimesQuality()
    {
        IndustrialKpiEngine.Oee(0.9375m, 0.8333m, 0.95m).ShouldBe(0.7422m);
        IndustrialKpiEngine.Oee(0.9m, 0.9m, 0.9m).ShouldBe(0.729m);
        IndustrialKpiEngine.Oee(0.9m, null, 0.9m).ShouldBeNull();
    }

    [Fact]
    public void ScrapRate_IsDefectiveUnitsOverTotal()
    {
        IndustrialKpiEngine.ScrapRate(1000m, 950m).ShouldBe(0.05m);
        IndustrialKpiEngine.ScrapRate(1000m, 1000m).ShouldBe(0m);
        IndustrialKpiEngine.ScrapRate(0m, 0m).ShouldBeNull();
    }

    [Fact]
    public void CostPerUnit_IsTotalCostOverUnitsProduced()
    {
        IndustrialKpiEngine.CostPerUnit(10_000m, 400m).ShouldBe(25m);
        IndustrialKpiEngine.CostPerUnit(10_000m, 3m).ShouldBe(3333.33m);
        IndustrialKpiEngine.CostPerUnit(10_000m, 0m).ShouldBeNull();
        IndustrialKpiEngine.CostPerUnit(null, 400m).ShouldBeNull();
    }

    [Fact]
    public void DowntimeRatio_AndCapacityUtilization()
    {
        IndustrialKpiEngine.DowntimeRatio(60m, 960m).ShouldBe(0.0625m);
        IndustrialKpiEngine.DowntimeRatio(60m, 0m).ShouldBeNull();
        // 750 min ideales sobre 960 planificados.
        IndustrialKpiEngine.CapacityUtilization(30m, 1500m, 960m).ShouldBe(0.7812m); // 0,78125 (redondeo bancario)
        IndustrialKpiEngine.CapacityUtilization(null, 1500m, 960m).ShouldBeNull();
    }

    [Fact]
    public void Losses_FollowTheSpecFormulas()
    {
        // downtime_cost = downtime_minutes × cost_per_minute
        IndustrialKpiEngine.DowntimeCost(60m, 50m).ShouldBe(3000m);
        IndustrialKpiEngine.DowntimeCost(60m, null).ShouldBeNull();
        // scrap_cost = (total_units − good_units) × unit_cost
        IndustrialKpiEngine.ScrapCost(1000m, 950m, 20m).ShouldBe(1000m);
        IndustrialKpiEngine.ScrapCost(1000m, 950m, null).ShouldBeNull();
        // velocidad: (marcha − ideal) × costo por minuto; nunca negativa
        IndustrialKpiEngine.SpeedLossCost(900m, 750m, 50m).ShouldBe(7500m);
        IndustrialKpiEngine.SpeedLossCost(700m, 750m, 50m).ShouldBe(0m);
        // total_impact = downtime_cost + scrap_cost
        new IndustrialLosses(3000m, 1000m, 7500m).Total.ShouldBe(4000m);
        new IndustrialLosses(null, 1000m, null).Total.ShouldBe(1000m);
        IndustrialLosses.None.Total.ShouldBeNull();
    }

    // --- Agregación ---

    private static readonly ProductionRecord[] Production =
    [
        new(new DateOnly(2024, 1, 10), "M1", "L1", 760m, 48m, 480m, 32m, 760m, 800m),
        new(new DateOnly(2024, 2, 10), "M1", "L1", 900m, 0m, 480m, 32m, 900m, 900m),
        new(new DateOnly(2024, 1, 10), "M2", "L2", 450m, 120m, 480m, null, 450m, 500m),
    ];

    private static readonly CostRecord[] Costs = [new(new DateOnly(2024, 1, 31), "M1", 1000m, 2000m, 500m, 500m)];

    private static readonly StandardRecord[] Standards = [new("M1", 30m, 5m, 10m)];

    private static IndustrialAggregate Find(IReadOnlyList<IndustrialAggregate> all, int? month, IndustrialLevel level, string? id = null) =>
        all.Single(a => a.Month == month && a.Level == level && (id is null || a.MachineId == id || (level == IndustrialLevel.Line && a.LineId == id)));

    [Fact]
    public void Aggregate_ProducesYearAndMonths_ForPlantLinesAndMachines()
    {
        var all = IndustrialKpiEngine.Aggregate(Production, Costs, Standards);

        all.Select(a => a.Period).Distinct().ShouldBe(["2024", "2024-01", "2024-02"]);
        all.Count(a => a.Month == 1).ShouldBe(5); // planta + 2 líneas + 2 máquinas
        all.Count(a => a.Month == 2).ShouldBe(3); // planta + L1 + M1
        all.Count(a => a.Month is null).ShouldBe(5);
        Find(all, null, IndustrialLevel.Machine, "M2").LineId.ShouldBe("L2");
    }

    [Fact]
    public void Machine_KpisAndLosses_UseItsStandardAndCosts()
    {
        var m1 = Find(IndustrialKpiEngine.Aggregate(Production, Costs, Standards), 1, IndustrialLevel.Machine, "M1");

        m1.Kpis.Availability.ShouldBe(0.9m);            // (480 − 48) / 480
        m1.Kpis.Performance.ShouldBe(0.9259m);          // 30 s × 800 / 60 = 400 min / 432 min
        m1.Kpis.Quality.ShouldBe(0.95m);                // 760 / 800
        m1.Kpis.Oee.ShouldBe(IndustrialKpiEngine.Oee(0.9m, 0.9259m, 0.95m));
        m1.Kpis.CostPerUnit.ShouldBe(5.26m);            // 4.000 / 760
        m1.Kpis.StandardCostPerUnit.ShouldBe(5m);
        m1.Losses.DowntimeCost.ShouldBe(480m);          // 48 min × 10
        m1.Losses.ScrapCost.ShouldBe(200m);             // 40 unidades × 5
        m1.Losses.SpeedLossCost.ShouldBe(320m);         // (432 − 400) min × 10
        m1.Sums.Costs!.Total.ShouldBe(4000m);
    }

    [Fact]
    public void Machine_WithoutStandard_HasNoPerformanceNorOee_ButKeepsOtherKpis()
    {
        var m2 = Find(IndustrialKpiEngine.Aggregate(Production, Costs, Standards), 1, IndustrialLevel.Machine, "M2");

        m2.Kpis.Availability.ShouldBe(0.75m);
        m2.Kpis.Quality.ShouldBe(0.9m);
        m2.Kpis.Performance.ShouldBeNull();
        m2.Kpis.Oee.ShouldBeNull();
        m2.Kpis.CostPerUnit.ShouldBeNull();
        m2.Losses.ShouldBe(IndustrialLosses.None);
    }

    [Fact]
    public void Plant_IsCalculatedFromSums_NotByAveragingRatios()
    {
        var plant = Find(IndustrialKpiEngine.Aggregate(Production, Costs, Standards), null, IndustrialLevel.Total);

        plant.Sums.PlannedMinutes.ShouldBe(1440m);
        plant.Sums.DowntimeMinutes.ShouldBe(168m);
        plant.Sums.TotalUnits.ShouldBe(2200m);
        plant.Sums.Days.ShouldBe(2);
        plant.Kpis.Availability.ShouldBe(0.8833m);     // 1.272 / 1.440
        plant.Kpis.Quality.ShouldBe(0.9591m);          // 2.110 / 2.200
        plant.Kpis.Performance.ShouldBe(0.932m);       // solo las máquinas con estándar: 850 / 912
        plant.Kpis.ScrapRate.ShouldBe(0.0409m);
        plant.Kpis.CostPerUnit.ShouldBe(5.26m);        // costos de enero de M1 / unidades de M1 en enero (febrero no tiene costos)
        plant.Sums.UnitsWithCost.ShouldBe(760m);
        plant.Losses.DowntimeCost.ShouldBe(480m);      // M1 (enero, febrero sin paradas); M2 sin costo por minuto
    }

    [Fact]
    public void Line_AggregatesItsMachines()
    {
        var all = IndustrialKpiEngine.Aggregate(Production, Costs, Standards);
        var line = Find(all, 1, IndustrialLevel.Line, "L1");

        line.LineId.ShouldBe("L1");
        line.MachineId.ShouldBeNull();
        line.Kpis.Oee.ShouldBe(Find(all, 1, IndustrialLevel.Machine, "M1").Kpis.Oee);
    }

    [Fact]
    public void WithoutStandard_CostPerMinuteAndUnitCostComeFromActualCosts()
    {
        var m2Costs = new CostRecord[] { new(new DateOnly(2024, 1, 31), "M2", 2400m, 0m, 0m, 0m) };
        var m2 = Find(IndustrialKpiEngine.Aggregate(Production, m2Costs, []), 1, IndustrialLevel.Machine, "M2");

        m2.CostPerMinute.ShouldBe(5m);                 // 2.400 / 480 min planificados
        m2.UnitCost.ShouldBe(4.8m);                    // 2.400 / 500 unidades procesadas
        m2.Losses.DowntimeCost.ShouldBe(600m);         // 120 × 5
        m2.Losses.ScrapCost.ShouldBe(240m);            // 50 × 4,8
        m2.Losses.SpeedLossCost.ShouldBeNull();        // sin ciclo estándar
    }

    [Fact]
    public void Daily_CombinesTheMachinesOfEachDay()
    {
        var daily = IndustrialKpiEngine.Daily(Production, Standards);

        daily.Select(d => d.Date).ShouldBe([new DateOnly(2024, 1, 10), new DateOnly(2024, 2, 10)]);
        var first = daily[0];
        first.DowntimeMinutes.ShouldBe(168m);
        first.Availability.ShouldBe(0.825m);           // (960 − 168) / 960
        first.Oee.ShouldBe(IndustrialKpiEngine.Oee(first.Availability, first.Performance, first.Quality));
        daily[1].Oee.ShouldBe(IndustrialKpiEngine.Oee(1m, 0.9375m, 1m)); // 450 min ideales / 480
    }

    [Fact]
    public void Aggregate_WithoutProduction_IsEmpty() =>
        IndustrialKpiEngine.Aggregate([], Costs, Standards).ShouldBeEmpty();

    [Fact]
    public void MachineIds_AreCaseInsensitive()
    {
        var all = IndustrialKpiEngine.Aggregate(
            [new(new DateOnly(2024, 1, 10), "maq-1", "L1", 10m, 0m, 60m, null, 10m, 10m)],
            [],
            [new("MAQ-1", 360m, null, null)]);

        Find(all, null, IndustrialLevel.Machine).Kpis.Performance.ShouldBe(1m); // 360 s × 10 = 60 min
    }
}
