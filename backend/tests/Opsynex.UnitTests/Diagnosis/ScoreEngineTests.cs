using Opsynex.Domain.Engines.Kpis;
using Opsynex.Domain.Engines.Scoring;
using Opsynex.Domain.Enums;

namespace Opsynex.UnitTests.Diagnosis;

public sealed class ScoreEngineTests
{
    private static readonly ScoreSettings Settings = new();

    /// <summary>Todos los insumos de cada componente con el mismo puntaje.</summary>
    private static ScoreInput Input(decimal? profitability, decimal? cost, decimal? productivity, decimal? stability) => new()
    {
        Normalized = new Dictionary<KpiCode, decimal?>
        {
            [KpiCode.EbitdaMargin] = profitability,
            [KpiCode.GrossMargin] = profitability,
            [KpiCode.CostEfficiency] = cost,
            [KpiCode.OperatingCostRatio] = cost,
            [KpiCode.RevenuePerEmployee] = productivity,
            [KpiCode.CurrentRatio] = stability,
            [KpiCode.DebtToEquity] = stability,
        },
    };

    [Fact]
    public void Total_IsWeightedAverage_WithSpecWeights()
    {
        // 0,30 × 80 + 0,25 × 60 + 0,25 × 40 + 0,20 × 20 = 53.
        var result = ScoreEngine.Calculate(Input(80m, 60m, 40m, 20m), Settings);

        result.Total.ShouldBe(53m);
        result.Interpretation.ShouldBe(ScoreInterpretation.Deteriorated);
        result.WeightsRedistributed.ShouldBeFalse();
        result.Components.Select(c => c.Weight).ShouldBe([0.30m, 0.25m, 0.25m, 0.20m]);
        result.Component(KpiDimension.Profitability).Value.ShouldBe(80m);
    }

    [Fact]
    public void MissingComponent_RedistributesItsWeightProportionally()
    {
        // Sin productividad: (0,30 × 80 + 0,25 × 60 + 0,20 × 20) / 0,75 = 57,33.
        var result = ScoreEngine.Calculate(Input(80m, 60m, null, 20m), Settings);

        result.Total.ShouldBe(57.33m);
        result.WeightsRedistributed.ShouldBeTrue();
        var productivity = result.Component(KpiDimension.Productivity);
        productivity.Value.ShouldBeNull();
        productivity.Weight.ShouldBe(0m);
        productivity.BaseWeight.ShouldBe(0.25m);
        productivity.MissingReason!.ShouldContain("empleados");
        result.Component(KpiDimension.Profitability).Weight.ShouldBe(0.4m);
        result.Component(KpiDimension.CostEfficiency).Weight.ShouldBe(0.3333m);
        result.Component(KpiDimension.FinancialStability).Weight.ShouldBe(0.2667m);
    }

    [Fact]
    public void Component_AveragesOnlyItsAvailableInputs()
    {
        var input = Input(null, 50m, null, null) with
        {
            Normalized = new Dictionary<KpiCode, decimal?> { [KpiCode.EbitdaMargin] = 90m, [KpiCode.GrossMargin] = null, [KpiCode.CostEfficiency] = 50m },
        };

        var result = ScoreEngine.Calculate(input, Settings);

        result.Component(KpiDimension.Profitability).Value.ShouldBe(90m);
        result.Component(KpiDimension.Profitability).Inputs[nameof(KpiCode.GrossMargin)].ShouldBeNull();
        result.Total.ShouldBe(decimal.Round(((0.30m * 90m) + (0.25m * 50m)) / 0.55m, 2));
    }

    [Fact]
    public void NoData_HasNoScore()
    {
        var result = ScoreEngine.Calculate(Input(null, null, null, null), Settings);

        result.Total.ShouldBeNull();
        result.Interpretation.ShouldBeNull();
        result.Components.ShouldAllBe(c => c.Weight == 0m && c.MissingReason != null);
    }

    [Fact]
    public void Total_StaysBetween0And100()
    {
        ScoreEngine.Calculate(Input(100m, 100m, 100m, 100m), Settings).Total.ShouldBe(100m);
        ScoreEngine.Calculate(Input(0m, 0m, 0m, 0m), Settings).Total.ShouldBe(0m);
    }

    [Theory]
    [InlineData(100, ScoreInterpretation.Healthy)]
    [InlineData(80, ScoreInterpretation.Healthy)]
    [InlineData(79.99, ScoreInterpretation.Improvable)]
    [InlineData(60, ScoreInterpretation.Improvable)]
    [InlineData(59.99, ScoreInterpretation.Deteriorated)]
    [InlineData(0, ScoreInterpretation.Deteriorated)]
    public void Interpretation_FollowsSpecBands(double score, ScoreInterpretation expected) =>
        ScoreEngine.Interpret((decimal)score).ShouldBe(expected);

    [Fact]
    public void Labels_AreInSpanish()
    {
        ScoreEngine.Label(ScoreInterpretation.Healthy).ShouldBe("Operación saludable");
        ScoreEngine.Label(ScoreInterpretation.Improvable).ShouldBe("Oportunidades de mejora");
        ScoreEngine.Label(ScoreInterpretation.Deteriorated).ShouldBe("Deterioro operacional significativo");
    }

    [Theory]
    [InlineData(100, 110, 100)]  // +10 % → 100
    [InlineData(100, 105, 75)]   // +5 %
    [InlineData(100, 100, 50)]   // estable
    [InlineData(100, 90, 0)]     // −10 % → 0
    [InlineData(100, 50, 0)]     // limitado a 0
    public void ProductivityTrend_MapsGrowthTo0To100(double previous, double current, double expected) =>
        ScoreEngine.TrendScore((decimal)previous, (decimal)current, 10m).ShouldBe((decimal)expected);

    [Fact]
    public void Productivity_CombinesLevelAndTrend()
    {
        // 70 % nivel (50) + 30 % tendencia (+10 % → 100) = 65.
        var input = Input(null, null, 50m, null) with { RevenuePerEmployee = 11m, PreviousRevenuePerEmployee = 10m };

        var productivity = ScoreEngine.Calculate(input, Settings).Component(KpiDimension.Productivity);

        productivity.Value.ShouldBe(65m);
        productivity.Inputs[ScoreEngine.ProductivityTrend].ShouldBe(100m);
    }

    [Fact]
    public void MarginVolatility_PenalizesUnstableMargins()
    {
        ScoreEngine.Volatility([0.20m, 0.20m], Settings).ShouldBe(100m);
        ScoreEngine.Volatility([0.15m, 0.25m], Settings).ShouldBe(0m);        // desviación de 5 pp
        ScoreEngine.Volatility([0.18m, 0.20m, 0.22m], Settings).ShouldBe(67.34m); // 1,63 pp
        ScoreEngine.Volatility([0.50m, 0.18m, 0.20m, 0.22m], Settings).ShouldBe(67.34m); // solo los últimos 3 años
        ScoreEngine.Volatility([0.20m], Settings).ShouldBeNull();             // un solo año
    }

    [Fact]
    public void FinancialStability_IncludesVolatility()
    {
        var input = Input(null, null, null, 60m) with { EbitdaMarginHistory = [0.20m, 0.20m] };

        var stability = ScoreEngine.Calculate(input, Settings).Component(KpiDimension.FinancialStability);

        stability.Value.ShouldBe(decimal.Round((60m + 60m + 100m) / 3m, 2));
        stability.Inputs[ScoreEngine.MarginVolatility].ShouldBe(100m);
    }

    [Fact]
    public void Settings_Validation()
    {
        new ScoreSettings().Validate().ShouldBeEmpty();
        new ScoreSettings { Weights = new() { ["Profitability"] = 0.5m, ["CostEfficiency"] = 0.25m } }.Validate().ShouldNotBeEmpty();
        new ScoreSettings { Weights = new() { ["Profitability"] = 1m, ["Unknown"] = 0m } }.Validate().ShouldNotBeEmpty();
        new ScoreSettings { MarginVolatilityWindowYears = 1 }.Validate().ShouldNotBeEmpty();
    }
}
