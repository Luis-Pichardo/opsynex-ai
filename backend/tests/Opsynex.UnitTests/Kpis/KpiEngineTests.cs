using Opsynex.Domain.Engines.Kpis;
using Opsynex.Domain.Entities;

namespace Opsynex.UnitTests.Kpis;

public sealed class KpiEngineTests
{
    private static decimal? Kpi(KpiInput input, KpiCode code) => KpiEngine.Calculate(input)[code];

    private static decimal? D(object? value) => value is null ? null : Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture);

    // (revenue, numerador, esperado) para los KPIs "X / Revenue".
    public static TheoryData<KpiCode, double?, double?, double?> RevenueRatios => new()
    {
        { KpiCode.EbitdaMargin, 1000, 150, 0.15 },
        { KpiCode.EbitdaMargin, 1000, -50, -0.05 },     // EBITDA negativo: margen negativo
        { KpiCode.EbitdaMargin, 0, 150, null },         // ingresos en cero
        { KpiCode.EbitdaMargin, -1000, 150, null },     // ingresos negativos: sin interpretación
        { KpiCode.EbitdaMargin, null, 150, null },
        { KpiCode.EbitdaMargin, 1000, null, null },
        { KpiCode.CostEfficiency, 1000, 853.3, 0.8533 },
        { KpiCode.CostEfficiency, 3, 1, 0.3333 },       // redondeo a 4 decimales
        { KpiCode.OperatingCostRatio, 800, 100, 0.125 },
        { KpiCode.OverheadRatio, 2000, 90, 0.045 },
        { KpiCode.EnergyCostRatio, 2000, 0, 0 },
        { KpiCode.NetMargin, 1000, -120, -0.12 },
        { KpiCode.LaborCostRatio, 1000, 172.3, 0.1723 },
    };

    [Theory]
    [MemberData(nameof(RevenueRatios))]
    public void RatiosOverRevenue(KpiCode code, double? revenue, double? numerator, double? expected)
    {
        var n = (decimal?)numerator;
        var input = new KpiInput
        {
            Revenue = (decimal?)revenue,
            Ebitda = n,
            TotalCost = n,
            OperatingExpenses = n,
            OverheadCost = n,
            EnergyCost = n,
            NetIncome = n,
            LaborCost = n,
        };

        Kpi(input, code).ShouldBe((decimal?)expected);
    }

    [Theory]
    [InlineData(1000, 700, 0.3)]
    [InlineData(1000, 1200, -0.2)]   // costo mayor que ingresos: margen bruto negativo
    [InlineData(0, 700, null)]
    [InlineData(1000, null, null)]
    public void GrossMargin(object? revenue, object? cogs, object? expected) =>
        Kpi(new KpiInput { Revenue = D(revenue), Cogs = D(cogs) }, KpiCode.GrossMargin).ShouldBe(D(expected));

    [Theory]
    [InlineData(1_000_000, 15, 66666.67)]
    [InlineData(1_000_000, 0, null)]
    [InlineData(1_000_000, -3, null)]
    [InlineData(null, 15, null)]
    public void RevenuePerEmployee(object? revenue, int? employees, object? expected) =>
        Kpi(new KpiInput { Revenue = D(revenue), EmployeeCount = employees }, KpiCode.RevenuePerEmployee).ShouldBe(D(expected));

    [Theory]
    [InlineData(500_000, 1_000, 500)]
    [InlineData(500_000, 0, null)]
    [InlineData(500_000, null, null)]
    public void CostPerUnit(object? totalCost, object? units, object? expected) =>
        Kpi(new KpiInput { TotalCost = D(totalCost), UnitsProduced = D(units) }, KpiCode.CostPerUnit).ShouldBe(D(expected));

    [Theory]
    [InlineData(250, 100, 2.5)]
    [InlineData(250, 0, null)]
    [InlineData(-10, 100, -0.1)]
    public void CurrentRatio(object? assets, object? liabilities, object? expected) =>
        Kpi(new KpiInput { CurrentAssets = D(assets), CurrentLiabilities = D(liabilities) }, KpiCode.CurrentRatio)
            .ShouldBe(D(expected));

    [Theory]
    [InlineData(474, 1000, 0.474)]
    [InlineData(474, 0, null)]
    [InlineData(474, -200, null)]    // patrimonio negativo: no se calcula
    public void DebtToEquity(object? liabilities, object? equity, object? expected) =>
        Kpi(new KpiInput { TotalLiabilities = D(liabilities), Equity = D(equity) }, KpiCode.DebtToEquity)
            .ShouldBe(D(expected));

    [Theory]
    [InlineData(100, 1000, 36.5)]
    [InlineData(100, 0, null)]
    [InlineData(null, 1000, null)]
    public void DaysSalesOutstanding(object? receivable, object? revenue, object? expected) =>
        Kpi(new KpiInput { AccountsReceivable = D(receivable), Revenue = D(revenue) }, KpiCode.DaysSalesOutstanding)
            .ShouldBe(D(expected));

    [Theory]
    [InlineData(200, 730, 100)]
    [InlineData(1, 3, 121.67)]
    [InlineData(200, 0, null)]
    public void InventoryDays(object? inventory, object? cogs, object? expected) =>
        Kpi(new KpiInput { Inventory = D(inventory), Cogs = D(cogs) }, KpiCode.InventoryDays).ShouldBe(D(expected));

    [Fact]
    public void EmptyInput_ReturnsAllKpisAsNull()
    {
        var result = KpiEngine.Calculate(new KpiInput());

        result.Keys.ShouldBe(Enum.GetValues<KpiCode>(), ignoreOrder: true);
        result.Values.ShouldAllBe(v => v == null);
    }

    [Fact]
    public void Catalog_HasOneDefinitionPerKpi_AndKpiResultMapsEveryCode()
    {
        KpiCatalog.All.Select(d => d.Code).ShouldBe(Enum.GetValues<KpiCode>());
        KpiCatalog.Get(KpiCode.CostEfficiency).HigherIsBetter.ShouldBeFalse();
        KpiCatalog.Get(KpiCode.EbitdaMargin).HigherIsBetter.ShouldBeTrue();

        var values = Enum.GetValues<KpiCode>().ToDictionary(c => c, c => (decimal?)((int)c + 1));
        var result = new KpiResult();
        result.SetValues(values);
        result.Values().ShouldBe(values);
    }
}

public sealed class TrendAnalyzerTests
{
    [Theory]
    [InlineData(0.20, 0.18, true, TrendDirection.Down, false)]   // baja el margen: desfavorable
    [InlineData(0.85, 0.80, false, TrendDirection.Down, true)]   // baja la eficiencia de costos: favorable
    [InlineData(0.80, 0.85, false, TrendDirection.Up, false)]
    [InlineData(100, 100.9, true, TrendDirection.Stable, null)]  // ±1 %: estable
    [InlineData(-10, -5, true, TrendDirection.Up, true)]         // pérdida que se reduce
    [InlineData(0, 5, true, TrendDirection.Up, true)]            // sin base porcentual: dirección por el cambio absoluto
    public void Compare_DirectionAndFavorability(double previous, double current, bool higherIsBetter, TrendDirection direction, bool? favorable)
    {
        var trend = TrendAnalyzer.Compare((decimal)previous, (decimal)current, higherIsBetter);

        trend.Direction.ShouldBe(direction);
        trend.Favorable.ShouldBe(favorable);
        trend.Change.ShouldBe((decimal)current - (decimal)previous);
    }

    [Fact]
    public void Compare_ComputesPercentChange()
    {
        var trend = TrendAnalyzer.Compare(200m, 150m, higherIsBetter: true);

        trend.ChangePercent.ShouldBe(-25m);
    }

    [Fact]
    public void Compare_MissingValues_HaveNoDirection()
    {
        var trend = TrendAnalyzer.Compare(null, 0.2m, higherIsBetter: true);

        trend.Direction.ShouldBeNull();
        trend.Change.ShouldBeNull();
        trend.Current.ShouldBe(0.2m);
    }

    [Fact]
    public void Compare_UsesConfigurableThreshold()
    {
        TrendAnalyzer.Compare(100m, 104m, true, stableThresholdPercent: 5m).Direction.ShouldBe(TrendDirection.Stable);
        TrendAnalyzer.Compare(100m, 104m, true, stableThresholdPercent: 1m).Direction.ShouldBe(TrendDirection.Up);
    }

    [Fact]
    public void Analyze_IncludesEveryKpiAndGrowth()
    {
        var previous = new Dictionary<KpiCode, decimal?> { [KpiCode.EbitdaMargin] = 0.2m };
        var current = new Dictionary<KpiCode, decimal?> { [KpiCode.EbitdaMargin] = 0.18m };

        var trends = TrendAnalyzer.Analyze(2024, current, new GrowthInput(1100, 990, 60), 2023, previous, new GrowthInput(1000, 850, 50));

        trends.Kpis.Count.ShouldBe(KpiCatalog.All.Count);
        trends.Kpis[KpiCode.EbitdaMargin].Direction.ShouldBe(TrendDirection.Down);
        trends.RevenueGrowth!.ChangePercent.ShouldBe(10m);
        trends.TotalCostGrowth!.ChangePercent.ShouldBe(16.47m);
        trends.TotalCostGrowth.Favorable.ShouldBe(false);
        trends.OverheadGrowth!.ChangePercent.ShouldBe(20m);
    }
}

public sealed class KpiNormalizerTests
{
    [Theory]
    [InlineData(0.10, 0, 0.20, 50)]
    [InlineData(0.25, 0, 0.20, 100)]   // por encima del mejor: 100
    [InlineData(-0.05, 0, 0.20, 0)]    // por debajo del peor: 0
    [InlineData(0.80, 1.00, 0.75, 80)] // rango invertido (bajar es mejor)
    [InlineData(1.20, 1.00, 0.75, 0)]
    public void Normalize_LinearAndClamped(double value, double worst, double best, double expected) =>
        KpiNormalizer.Normalize((decimal)value, new BenchmarkRange((decimal)worst, (decimal)best)).ShouldBe((decimal)expected);

    [Fact]
    public void Normalize_WithoutValueOrBenchmark_IsNull()
    {
        KpiNormalizer.Normalize(null, new BenchmarkRange(0, 1)).ShouldBeNull();
        KpiNormalizer.Normalize(0.5m, null).ShouldBeNull();
        KpiNormalizer.Normalize(0.5m, new BenchmarkRange(1, 1)).ShouldBeNull();
    }
}

public sealed class MonthlyAnalyzerTests
{
    [Fact]
    public void Analyze_GrowthMovingAverageAndSeasonality()
    {
        var points = MonthlyAnalyzer.Analyze(
        [
            new MonthlyRevenue(2024, 3, 120),
            new MonthlyRevenue(2024, 1, 100),
            new MonthlyRevenue(2024, 2, 80),
            new MonthlyRevenue(2024, 4, 100),
        ]);

        points.Select(p => p.Month).ShouldBe([1, 2, 3, 4]);
        points[0].GrowthPercent.ShouldBeNull();
        points[1].GrowthPercent.ShouldBe(-20m);
        points[2].GrowthPercent.ShouldBe(50m);
        points[1].MovingAverage3.ShouldBeNull();
        points[2].MovingAverage3.ShouldBe(100m);
        points[3].MovingAverage3.ShouldBe(100m);
        points[2].SeasonalityIndex.ShouldBe(1.2m); // 120 / promedio 100
    }

    [Fact]
    public void Analyze_GapsBreakGrowthAndMovingAverage()
    {
        var points = MonthlyAnalyzer.Analyze(
        [
            new MonthlyRevenue(2024, 1, 100), new MonthlyRevenue(2024, 2, 110), new MonthlyRevenue(2024, 4, 130),
        ]);

        points[2].GrowthPercent.ShouldBeNull();
        points[2].MovingAverage3.ShouldBeNull();
    }

    [Fact]
    public void Analyze_ContinuesAcrossYears()
    {
        var points = MonthlyAnalyzer.Analyze([new MonthlyRevenue(2023, 12, 100), new MonthlyRevenue(2024, 1, 90)]);

        points[1].GrowthPercent.ShouldBe(-10m);
    }
}
