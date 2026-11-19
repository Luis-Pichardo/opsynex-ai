using Opsynex.Domain.Engines.Industrial;
using Opsynex.Domain.Enums;

namespace Opsynex.UnitTests.Industrial;

/// <summary>Score industrial (§8): producción 0,30 + costo 0,25 + financiero 0,25 + estándar 0,20.</summary>
public sealed class IndustrialScoreEngineTests
{
    private static readonly IndustrialSettings Settings = new();

    // OEE 62,5 % → 50; costo 1,15 × estándar → 50; rendimiento 80 % → 50; scrap 6 % → 50.
    private static IndustrialScoreInput Half(decimal? financial = 60m) => new(0.625m, 0.80m, 0.06m, 11.5m, 10m, financial);

    [Fact]
    public void Total_IsTheWeightedSumWithSpecWeights()
    {
        var result = IndustrialScoreEngine.Calculate(Half(), Settings);

        // 0,30 × 50 + 0,25 × 50 + 0,25 × 60 + 0,20 × 50 = 52,5.
        result.Total.ShouldBe(52.5m);
        result.Interpretation.ShouldBe(ScoreInterpretation.Deteriorated);
        result.WeightsRedistributed.ShouldBeFalse();
        result.Components.Select(c => c.Weight).ShouldBe([0.30m, 0.25m, 0.25m, 0.20m]);
        result.Component(IndustrialDimension.Production).Value.ShouldBe(50m);
        result.Component(IndustrialDimension.Cost).Value.ShouldBe(50m);
        result.Component(IndustrialDimension.Cost).Inputs["CostRatio"].ShouldBe(1.15m);
        result.Component(IndustrialDimension.Financial).Value.ShouldBe(60m);
        result.Component(IndustrialDimension.Standard).Value.ShouldBe(50m);
    }

    [Fact]
    public void WorldClassPlant_Scores100()
    {
        var result = IndustrialScoreEngine.Calculate(new IndustrialScoreInput(0.90m, 1.05m, 0.01m, 9m, 10m, 100m), Settings);

        result.Total.ShouldBe(100m);
        result.Interpretation.ShouldBe(ScoreInterpretation.Healthy);
    }

    [Fact]
    public void MissingFinancialScore_RedistributesItsWeight()
    {
        var result = IndustrialScoreEngine.Calculate(Half(financial: null), Settings);

        result.Total.ShouldBe(50m);
        result.WeightsRedistributed.ShouldBeTrue();
        var financial = result.Component(IndustrialDimension.Financial);
        financial.Value.ShouldBeNull();
        financial.Weight.ShouldBe(0m);
        financial.BaseWeight.ShouldBe(0.25m);
        financial.MissingReason!.ShouldContain("Score Opsynex");
        result.Component(IndustrialDimension.Production).Weight.ShouldBe(0.4m);
        result.Component(IndustrialDimension.Cost).Weight.ShouldBe(0.3333m);
        result.Component(IndustrialDimension.Standard).Weight.ShouldBe(0.2667m);
    }

    [Fact]
    public void WithoutStandardCost_TheCostComponentIsMissing()
    {
        var result = IndustrialScoreEngine.Calculate(Half() with { StandardCostPerUnit = null }, Settings);

        result.Component(IndustrialDimension.Cost).Value.ShouldBeNull();
        result.Total.ShouldBe(decimal.Round(((0.30m * 50m) + (0.25m * 60m) + (0.20m * 50m)) / 0.75m, 2));
    }

    [Fact]
    public void StandardComponent_AveragesOnlyWhatIsAvailable()
    {
        var result = IndustrialScoreEngine.Calculate(new IndustrialScoreInput(null, null, 0.02m, null, null, null), Settings);

        result.Component(IndustrialDimension.Standard).Value.ShouldBe(100m);
        result.Total.ShouldBe(100m);
    }

    [Fact]
    public void NoData_HasNoScore()
    {
        var result = IndustrialScoreEngine.Calculate(new IndustrialScoreInput(null, null, null, null, null, null), Settings);

        result.Total.ShouldBeNull();
        result.Interpretation.ShouldBeNull();
        result.Components.ShouldAllBe(c => c.Weight == 0m && c.MissingReason != null);
    }

    [Theory]
    [InlineData(0.40, 0)]
    [InlineData(0.30, 0)]    // limitado a 0
    [InlineData(0.85, 100)]
    [InlineData(0.95, 100)]  // limitado a 100
    [InlineData(0.6250, 50)]
    public void Normalize_IsLinearAndClamped(double oee, double expected) =>
        IndustrialScoreEngine.Normalize((decimal)oee, Settings.OeeRange).ShouldBe((decimal)expected);

    [Fact]
    public void Normalize_SupportsInvertedRanges()
    {
        // Scrap: menos es mejor (10 % → 0, 2 % → 100).
        IndustrialScoreEngine.Normalize(0.02m, Settings.ScrapRange).ShouldBe(100m);
        IndustrialScoreEngine.Normalize(0.10m, Settings.ScrapRange).ShouldBe(0m);
        IndustrialScoreEngine.Normalize(0.08m, Settings.ScrapRange).ShouldBe(25m);
        IndustrialScoreEngine.Normalize(null, Settings.ScrapRange).ShouldBeNull();
    }

    [Fact]
    public void Settings_Validation()
    {
        new IndustrialSettings().Validate().ShouldBeEmpty();
        new IndustrialSettings { LowOeeThreshold = 0m }.Validate().ShouldNotBeEmpty();
        new IndustrialSettings { HighDowntimeThreshold = 1.5m }.Validate().ShouldNotBeEmpty();
        new IndustrialSettings { CostAboveStandardTolerance = -0.1m }.Validate().ShouldNotBeEmpty();
        new IndustrialSettings { OeeRange = new ScoreRange(0.5m, 0.5m) }.Validate().ShouldNotBeEmpty();

        var weights = new IndustrialSettings();
        weights.ScoreWeights["Production"] = 0.5m;
        weights.Validate().ShouldContain(e => e.Contains("sumar 1", StringComparison.Ordinal));

        var unknown = new IndustrialSettings();
        unknown.ScoreWeights["Otro"] = 0m;
        unknown.Validate().ShouldContain(e => e.Contains("Otro", StringComparison.Ordinal));
    }
}
