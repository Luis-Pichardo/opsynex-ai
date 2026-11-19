using Opsynex.Domain.Engines.Diagnosis;
using Opsynex.Domain.Engines.Diagnosis.Rules;
using Opsynex.Domain.Engines.Kpis;
using Opsynex.Domain.Enums;

namespace Opsynex.UnitTests.Diagnosis;

/// <summary>Constructor de contextos de prueba: año 2024 contra 2023.</summary>
internal static class Ctx
{
    public static DiagnosisContext Build(
        Dictionary<KpiCode, decimal?>? current = null,
        Dictionary<KpiCode, decimal?>? previous = null,
        FinancialSnapshot? financial = null,
        FinancialSnapshot? previousFinancial = null,
        Industry industry = Industry.Manufacturing,
        DiagnosisSettings? settings = null)
    {
        var hasPrevious = previous is not null || previousFinancial is not null;
        return new DiagnosisContext
        {
            Year = 2024,
            PreviousYear = hasPrevious ? 2023 : null,
            Industry = industry,
            Kpis = current ?? [],
            PreviousKpis = hasPrevious ? previous ?? [] : null,
            Financial = financial ?? new FinancialSnapshot(),
            PreviousFinancial = hasPrevious ? previousFinancial ?? new FinancialSnapshot() : null,
            Settings = settings ?? new DiagnosisSettings(),
        };
    }

    public static Dictionary<KpiCode, decimal?> K(KpiCode code, decimal? value) => new() { [code] = value };
}

public sealed class MarginDeclineRulesTests
{
    [Theory]
    [InlineData(0.20, 0.14, Severity.High)]    // −6 pp
    [InlineData(0.20, 0.17, Severity.Medium)]  // −3 pp
    [InlineData(0.20, 0.19, Severity.Low)]     // −1 pp
    public void EbitdaDecline_Fires_WithSeverityByPoints(double previous, double current, Severity severity)
    {
        var result = new EbitdaDeclineRule().Evaluate(Ctx.Build(
            Ctx.K(KpiCode.EbitdaMargin, (decimal)current), Ctx.K(KpiCode.EbitdaMargin, (decimal)previous)));

        result.ShouldNotBeNull();
        result.Severity.ShouldBe(severity);
        result.RelatedKpi.ShouldBe(KpiCode.EbitdaMargin);
        result.Evidence["previousYear"]!.GetValue<int>().ShouldBe(2023);
        result.Description.ShouldContain("2023");
    }

    [Theory]
    [InlineData(0.20, 0.21)]   // sube
    [InlineData(0.20, 0.198)]  // −0,2 pp: bajo el umbral (0,5 pp)
    public void EbitdaDecline_DoesNotFire_WhenStableOrImproving(double previous, double current) =>
        new EbitdaDeclineRule().Evaluate(Ctx.Build(
            Ctx.K(KpiCode.EbitdaMargin, (decimal)current), Ctx.K(KpiCode.EbitdaMargin, (decimal)previous))).ShouldBeNull();

    [Fact]
    public void EbitdaDecline_DoesNotFire_WithoutPreviousYearOrData()
    {
        new EbitdaDeclineRule().Evaluate(Ctx.Build(Ctx.K(KpiCode.EbitdaMargin, 0.10m))).ShouldBeNull();
        new EbitdaDeclineRule().Evaluate(Ctx.Build(Ctx.K(KpiCode.EbitdaMargin, null), Ctx.K(KpiCode.EbitdaMargin, 0.2m))).ShouldBeNull();
    }

    [Fact]
    public void GrossMarginDecline_FiresAndDoesNotFire()
    {
        var rule = new GrossMarginDeclineRule();
        rule.Evaluate(Ctx.Build(Ctx.K(KpiCode.GrossMargin, 0.27m), Ctx.K(KpiCode.GrossMargin, 0.30m)))!.Severity.ShouldBe(Severity.Medium);
        rule.Evaluate(Ctx.Build(Ctx.K(KpiCode.GrossMargin, 0.31m), Ctx.K(KpiCode.GrossMargin, 0.30m))).ShouldBeNull();
        rule.ImpactCategory.ShouldBe(ImpactCategory.MarginLoss);
    }
}

public sealed class GrowthRulesTests
{
    private static FinancialSnapshot F(decimal? revenue = null, decimal? total = null, decimal? overhead = null, decimal? labor = null) =>
        new() { Revenue = revenue, TotalCost = total, OverheadCost = overhead, LaborCost = labor };

    [Theory]
    [InlineData(100, 107.58, 100, 107.41, Severity.Low)]   // +0,17 pp (caso del libro sintético 2024)
    [InlineData(100, 120, 100, 110, Severity.High)]        // +10 pp
    [InlineData(100, 116, 100, 110, Severity.Medium)]      // +6 pp
    public void OverheadOutpacesRevenue_Fires(double prevOverhead, double overhead, double prevRevenue, double revenue, Severity severity)
    {
        var result = new OverheadOutpacesRevenueRule().Evaluate(Ctx.Build(
            financial: F(revenue: (decimal)revenue, overhead: (decimal)overhead),
            previousFinancial: F(revenue: (decimal)prevRevenue, overhead: (decimal)prevOverhead)));

        result.ShouldNotBeNull();
        result.Severity.ShouldBe(severity);
        result.Evidence["gapPp"]!.GetValue<decimal>().ShouldBeGreaterThan(0);
    }

    [Theory]
    [InlineData(100, 105, 100, 110)]  // crece menos que los ingresos
    [InlineData(100, 110, 100, 110)]  // igual
    public void OverheadOutpacesRevenue_DoesNotFire(double prevOverhead, double overhead, double prevRevenue, double revenue) =>
        new OverheadOutpacesRevenueRule().Evaluate(Ctx.Build(
            financial: F(revenue: (decimal)revenue, overhead: (decimal)overhead),
            previousFinancial: F(revenue: (decimal)prevRevenue, overhead: (decimal)prevOverhead))).ShouldBeNull();

    [Fact]
    public void LaborCostOutpacesRevenue_FiresAndDoesNotFire()
    {
        var rule = new LaborCostOutpacesRevenueRule();
        rule.Evaluate(Ctx.Build(financial: F(revenue: 107.41m, labor: 109.66m), previousFinancial: F(revenue: 100, labor: 100)))!
            .Severity.ShouldBe(Severity.Low);
        rule.Evaluate(Ctx.Build(financial: F(revenue: 110, labor: 104), previousFinancial: F(revenue: 100, labor: 100))).ShouldBeNull();
        rule.Evaluate(Ctx.Build(financial: F(revenue: 110, labor: null), previousFinancial: F(revenue: 100, labor: 100))).ShouldBeNull();
    }

    [Theory]
    [InlineData(100, 101, 100, 112, Severity.High)]    // ingresos +1 % (estables), costo +12 %
    [InlineData(100, 99, 100, 106, Severity.Medium)]
    public void CostGrowthFlatRevenue_Fires(double prevRevenue, double revenue, double prevCost, double cost, Severity severity) =>
        new CostGrowthFlatRevenueRule().Evaluate(Ctx.Build(
            financial: F(revenue: (decimal)revenue, total: (decimal)cost),
            previousFinancial: F(revenue: (decimal)prevRevenue, total: (decimal)prevCost)))!.Severity.ShouldBe(severity);

    [Theory]
    [InlineData(100, 108, 100, 112)]   // los ingresos no están estables (+8 %)
    [InlineData(100, 101, 100, 101)]   // el costo no sube por encima del umbral
    public void CostGrowthFlatRevenue_DoesNotFire(double prevRevenue, double revenue, double prevCost, double cost) =>
        new CostGrowthFlatRevenueRule().Evaluate(Ctx.Build(
            financial: F(revenue: (decimal)revenue, total: (decimal)cost),
            previousFinancial: F(revenue: (decimal)prevRevenue, total: (decimal)prevCost))).ShouldBeNull();

    [Theory]
    [InlineData(100, 80, Severity.High)]
    [InlineData(100, 95, Severity.Low)]
    public void RevenueDecline_Fires(double previous, double current, Severity severity) =>
        new RevenueDeclineRule().Evaluate(Ctx.Build(
            financial: F(revenue: (decimal)current), previousFinancial: F(revenue: (decimal)previous)))!.Severity.ShouldBe(severity);

    [Theory]
    [InlineData(100, 99)]
    [InlineData(100, 120)]
    public void RevenueDecline_DoesNotFire(double previous, double current) =>
        new RevenueDeclineRule().Evaluate(Ctx.Build(
            financial: F(revenue: (decimal)current), previousFinancial: F(revenue: (decimal)previous))).ShouldBeNull();

    [Fact]
    public void ProductivityDecline_FiresAndDoesNotFire()
    {
        var rule = new ProductivityDeclineRule();
        rule.Evaluate(Ctx.Build(Ctx.K(KpiCode.RevenuePerEmployee, 900_000), Ctx.K(KpiCode.RevenuePerEmployee, 1_000_000)))!
            .Severity.ShouldBe(Severity.Medium);
        rule.Evaluate(Ctx.Build(Ctx.K(KpiCode.RevenuePerEmployee, 990_000), Ctx.K(KpiCode.RevenuePerEmployee, 1_000_000))).ShouldBeNull();
        rule.Evaluate(Ctx.Build(Ctx.K(KpiCode.RevenuePerEmployee, 900_000), Ctx.K(KpiCode.RevenuePerEmployee, null))).ShouldBeNull();
    }

    [Fact]
    public void EnergyCostSpike_FiresAbove10PercentOnly()
    {
        var rule = new EnergyCostSpikeRule();
        rule.Evaluate(Ctx.Build(Ctx.K(KpiCode.EnergyCostRatio, 0.066m), Ctx.K(KpiCode.EnergyCostRatio, 0.05m)))!
            .Severity.ShouldBe(Severity.High);   // +32 %
        rule.Evaluate(Ctx.Build(Ctx.K(KpiCode.EnergyCostRatio, 0.0617m), Ctx.K(KpiCode.EnergyCostRatio, 0.0582m))).ShouldBeNull(); // +6 %
        rule.ImpactCategory.ShouldBe(ImpactCategory.OperationalWaste);
    }
}

public sealed class LevelRulesTests
{
    [Theory]
    [InlineData(Industry.Manufacturing, 0.04, Severity.High)]    // 6 pp bajo el 10 %
    [InlineData(Industry.Manufacturing, 0.09, Severity.Low)]
    [InlineData(Industry.Services, 0.09, Severity.Medium)]       // 3 pp bajo el 12 %
    public void LowEbitdaMargin_Fires(Industry industry, double margin, Severity severity) =>
        new LowEbitdaMarginRule().Evaluate(Ctx.Build(Ctx.K(KpiCode.EbitdaMargin, (decimal)margin), industry: industry))!
            .Severity.ShouldBe(severity);

    [Fact]
    public void LowEbitdaMargin_DoesNotFire_AboveBenchmarkOrWithoutData()
    {
        new LowEbitdaMarginRule().Evaluate(Ctx.Build(Ctx.K(KpiCode.EbitdaMargin, 0.186m))).ShouldBeNull();
        new LowEbitdaMarginRule().Evaluate(Ctx.Build()).ShouldBeNull();
    }

    [Fact]
    public void HighOpexRatio_FiresAndDoesNotFire()
    {
        new HighOpexRatioRule().Evaluate(Ctx.Build(Ctx.K(KpiCode.OperatingCostRatio, 0.36m)))!.Severity.ShouldBe(Severity.High);
        new HighOpexRatioRule().Evaluate(Ctx.Build(Ctx.K(KpiCode.OperatingCostRatio, 0.13m))).ShouldBeNull();
    }

    [Fact]
    public void HighMaterialLoss_FiresAndDoesNotFire()
    {
        new HighMaterialLossRule().Evaluate(Ctx.Build(financial: new FinancialSnapshot { MaterialLoss = 60, Cogs = 1000 }))!
            .Severity.ShouldBe(Severity.Medium); // 6 % vs 3 %: +3 pp
        new HighMaterialLossRule().Evaluate(Ctx.Build(financial: new FinancialSnapshot { MaterialLoss = 20, Cogs = 1000 })).ShouldBeNull();
        new HighMaterialLossRule().Evaluate(Ctx.Build(financial: new FinancialSnapshot { MaterialLoss = 20 })).ShouldBeNull();
    }

    [Theory]
    [InlineData(0.6, Severity.High)]
    [InlineData(0.85, Severity.Medium)]
    [InlineData(0.95, Severity.Low)]
    public void LowLiquidity_Fires(double ratio, Severity severity)
    {
        var result = new LowLiquidityRule().Evaluate(Ctx.Build(Ctx.K(KpiCode.CurrentRatio, (decimal)ratio)))!;

        result.Severity.ShouldBe(severity);
        new LowLiquidityRule().ImpactCategory.ShouldBe(ImpactCategory.FinancialAlert);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.77)]
    public void LowLiquidity_DoesNotFire(double ratio) =>
        new LowLiquidityRule().Evaluate(Ctx.Build(Ctx.K(KpiCode.CurrentRatio, (decimal)ratio))).ShouldBeNull();

    [Fact]
    public void HighLeverage_FiresAndDoesNotFire()
    {
        new HighLeverageRule().Evaluate(Ctx.Build(Ctx.K(KpiCode.DebtToEquity, 5.5m)))!.Severity.ShouldBe(Severity.High);
        new HighLeverageRule().Evaluate(Ctx.Build(Ctx.K(KpiCode.DebtToEquity, 3.4m)))!.Severity.ShouldBe(Severity.Low);
        new HighLeverageRule().Evaluate(Ctx.Build(Ctx.K(KpiCode.DebtToEquity, 0.47m))).ShouldBeNull();
    }
}

public sealed class DiagnosisEngineTests
{
    private static DiagnosisEngine Engine() => new(typeof(IDiagnosticRule).Assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IDiagnosticRule).IsAssignableFrom(t))
        .Select(t => (IDiagnosticRule)Activator.CreateInstance(t)!));

    [Fact]
    public void AllRules_AreDiscovered_WithUniqueCodesAndDefaults()
    {
        var codes = Engine().Rules.Select(r => r.Code).ToList();

        codes.Count.ShouldBe(13);
        codes.ShouldBeUnique();
        codes.ShouldAllBe(c => DiagnosisSettings.Defaults.ContainsKey(c));
    }

    [Fact]
    public void Evaluate_OrdersBySeverity_AndRespectsDisabledRules()
    {
        var context = Ctx.Build(
            new Dictionary<KpiCode, decimal?> { [KpiCode.EbitdaMargin] = 0.04m, [KpiCode.CurrentRatio] = 0.95m },
            new Dictionary<KpiCode, decimal?> { [KpiCode.EbitdaMargin] = 0.05m });

        var result = Engine().Evaluate(context);
        result.Problems.Select(p => p.Code).ShouldBe(["LOW_EBITDA_MARGIN", "EBITDA_DECLINE", "LOW_LIQUIDITY"]);
        result.Problems[0].Severity.ShouldBe(Severity.High);

        var settings = new DiagnosisSettings();
        settings.Rules["LOW_EBITDA_MARGIN"] = new RuleSettings { Enabled = false };
        Engine().Evaluate(context with { Settings = settings }).Problems.ShouldNotContain(p => p.Code == "LOW_EBITDA_MARGIN");
    }

    [Fact]
    public void Settings_OverrideThresholds()
    {
        var settings = new DiagnosisSettings();
        settings.Rules["EBITDA_DECLINE"] = new RuleSettings(3m, 4m, 6m);
        var context = Ctx.Build(Ctx.K(KpiCode.EbitdaMargin, 0.18m), Ctx.K(KpiCode.EbitdaMargin, 0.20m), settings: settings);

        new EbitdaDeclineRule().Evaluate(context).ShouldBeNull(); // −2 pp no llega al umbral configurado (3 pp)
    }

    [Fact]
    public void Prioritize_BySeverityThenImpact()
    {
        var items = new[] { (Severity.Medium, 900m), (Severity.High, (decimal?)null), (Severity.High, 10m), (Severity.Medium, 50m) }
            .Select(x => (Severity: x.Item1, Impact: (decimal?)x.Item2));

        DiagnosisEngine.Prioritize(items, i => i.Severity, i => i.Impact).ToList()
            .ShouldBe([(Severity.High, 10m), (Severity.High, null), (Severity.Medium, 900m), (Severity.Medium, 50m)]);
    }

    [Fact]
    public void Highlights_RecordPositiveFindings()
    {
        var context = Ctx.Build(
            new Dictionary<KpiCode, decimal?> { [KpiCode.EbitdaMargin] = 0.22m, [KpiCode.DebtToEquity] = 0.47m },
            new Dictionary<KpiCode, decimal?> { [KpiCode.EbitdaMargin] = 0.20m, [KpiCode.DebtToEquity] = 0.60m },
            new FinancialSnapshot { Revenue = 110 },
            new FinancialSnapshot { Revenue = 100 });

        var highlights = Engine().Evaluate(context).Highlights.Select(h => h.Code).ToList();

        highlights.ShouldBe(["EbitdaMargin", "DebtToEquity", "RevenueGrowth"], ignoreOrder: true);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(0.3, null)]
    [InlineData(0.5, Severity.Low)]
    [InlineData(2, Severity.Medium)]
    [InlineData(5, Severity.High)]
    public void RuleSettings_Classify(double magnitude, Severity? expected) =>
        new RuleSettings(0.5m, 2m, 5m).Classify((decimal)magnitude).ShouldBe(expected);
}
