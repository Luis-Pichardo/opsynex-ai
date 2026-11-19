using Opsynex.Domain.Engines.Diagnosis;
using Opsynex.Domain.Engines.Impact;
using Opsynex.Domain.Engines.Kpis;
using Opsynex.Domain.Enums;

namespace Opsynex.UnitTests.Diagnosis;

public sealed class ImpactEngineTests
{
    private static ImpactEstimate Estimate(string code, ImpactCategory category, DiagnosisContext context) =>
        ImpactEngine.Estimate(code, category, Severity.Medium, context);

    private static Dictionary<KpiCode, decimal?> K(params (KpiCode Code, decimal? Value)[] values) =>
        values.ToDictionary(v => v.Code, v => v.Value);

    // --- MarginLoss ---

    [Fact]
    public void EbitdaDecline_IsMarginDropTimesRevenue()
    {
        // (20 % − 17 %) × 1.000.000 = 30.000; 3 pp de margen; el margen cayó 15 % en términos relativos.
        var impact = Estimate("EBITDA_DECLINE", ImpactCategory.MarginLoss, Ctx.Build(
            K((KpiCode.EbitdaMargin, 0.17m)), K((KpiCode.EbitdaMargin, 0.20m)),
            new FinancialSnapshot { Revenue = 1_000_000m }));

        impact.EstimatedLoss.ShouldBe(30_000m);
        impact.AffectedMarginPp.ShouldBe(3m);
        impact.DeteriorationMagnitude.ShouldBe(15m);
        impact.SeverityLevel.ShouldBe(Severity.Medium);
        impact.Reason.ShouldBeNull();
        impact.Inputs["previousMargin"]!.GetValue<decimal>().ShouldBe(0.20m);
    }

    [Fact]
    public void GrossMarginDecline_IsMarginDropTimesRevenue() =>
        Estimate("GROSS_MARGIN_DECLINE", ImpactCategory.MarginLoss, Ctx.Build(
            K((KpiCode.GrossMargin, 0.27m)), K((KpiCode.GrossMargin, 0.30m)),
            new FinancialSnapshot { Revenue = 2_000_000m })).EstimatedLoss.ShouldBe(60_000m);

    [Fact]
    public void LowEbitdaMargin_IsGapToIndustryBenchmarkTimesRevenue()
    {
        // Referencia de manufactura 10 %: (10 % − 6 %) × 1.000.000 = 40.000.
        var impact = Estimate("LOW_EBITDA_MARGIN", ImpactCategory.MarginLoss, Ctx.Build(
            K((KpiCode.EbitdaMargin, 0.06m)), financial: new FinancialSnapshot { Revenue = 1_000_000m }));

        impact.EstimatedLoss.ShouldBe(40_000m);
        impact.DeteriorationMagnitude.ShouldBe(40m);
    }

    [Fact]
    public void RevenueDecline_IsLostRevenueTimesPreviousGrossMargin_OrEbitdaMarginAsFallback()
    {
        var withGross = Estimate("REVENUE_DECLINE", ImpactCategory.MarginLoss, Ctx.Build(
            K(), K((KpiCode.GrossMargin, 0.30m), (KpiCode.EbitdaMargin, 0.15m)),
            new FinancialSnapshot { Revenue = 900_000m }, new FinancialSnapshot { Revenue = 1_000_000m }));
        withGross.EstimatedLoss.ShouldBe(30_000m);
        withGross.DeteriorationMagnitude.ShouldBe(10m);

        Estimate("REVENUE_DECLINE", ImpactCategory.MarginLoss, Ctx.Build(
            K(), K((KpiCode.EbitdaMargin, 0.15m)),
            new FinancialSnapshot { Revenue = 900_000m }, new FinancialSnapshot { Revenue = 1_000_000m })).EstimatedLoss.ShouldBe(15_000m);
    }

    // --- ExcessCost ---

    [Theory]
    [InlineData("OVERHEAD_OUTPACES_REVENUE")]
    [InlineData("LABOR_COST_OUTPACES_REVENUE")]
    public void CostOutpacingRevenue_IsGrowthGapTimesPreviousCost(string code)
    {
        // Costo +20 % e ingresos +10 % → 10 % × 100.000 = 10.000.
        var previous = new FinancialSnapshot { Revenue = 1_000_000m, OverheadCost = 100_000m, LaborCost = 100_000m };
        var current = new FinancialSnapshot { Revenue = 1_100_000m, OverheadCost = 120_000m, LaborCost = 120_000m };

        var impact = Estimate(code, ImpactCategory.ExcessCost, Ctx.Build(financial: current, previousFinancial: previous));

        impact.EstimatedLoss.ShouldBe(10_000m);
        impact.DeteriorationMagnitude.ShouldBe(10m);
        impact.AffectedMarginPp.ShouldBe(0.91m); // 10.000 / 1.100.000
    }

    [Fact]
    public void CostGrowthWithFlatRevenue_IsGrowthGapTimesPreviousTotalCost() =>
        // Costo +5 %, ingresos +1 % → 4 % × 800.000 = 32.000.
        Estimate("COST_GROWTH_FLAT_REVENUE", ImpactCategory.ExcessCost, Ctx.Build(
            financial: new FinancialSnapshot { Revenue = 1_010_000m, TotalCost = 840_000m },
            previousFinancial: new FinancialSnapshot { Revenue = 1_000_000m, TotalCost = 800_000m })).EstimatedLoss.ShouldBe(32_000m);

    [Fact]
    public void HighOpexRatio_IsExcessOverBenchmarkTimesRevenue()
    {
        // Referencia de manufactura 25 %: (30 % − 25 %) × 1.000.000 = 50.000.
        var impact = Estimate("HIGH_OPEX_RATIO", ImpactCategory.ExcessCost, Ctx.Build(
            K((KpiCode.OperatingCostRatio, 0.30m)), financial: new FinancialSnapshot { Revenue = 1_000_000m }));

        impact.EstimatedLoss.ShouldBe(50_000m);
        impact.DeteriorationMagnitude.ShouldBe(20m);
    }

    // --- ProductivityLoss ---

    [Fact]
    public void ProductivityDecline_IsRevenuePerEmployeeDropTimesEmployees()
    {
        // (10 M − 9 M) × 20 empleados = 20 M.
        var context = Ctx.Build(
            K((KpiCode.RevenuePerEmployee, 9_000_000m)), K((KpiCode.RevenuePerEmployee, 10_000_000m)),
            new FinancialSnapshot { Revenue = 180_000_000m, EmployeeCount = 20 });
        var impact = Estimate("PRODUCTIVITY_DECLINE", ImpactCategory.ProductivityLoss, context);
        impact.EstimatedLoss.ShouldBe(20_000_000m);
        impact.DeteriorationMagnitude.ShouldBe(10m);

        // Sin la cantidad de empleados, se deduce de ingresos / ingresos por empleado.
        Estimate("PRODUCTIVITY_DECLINE", ImpactCategory.ProductivityLoss, context with
        {
            Financial = new FinancialSnapshot { Revenue = 180_000_000m },
        }).EstimatedLoss.ShouldBe(20_000_000m);
    }

    // --- OperationalWaste ---

    [Fact]
    public void MaterialLoss_IsExcessOverThresholdTimesCogs() =>
        // 5 % de mermas frente a un umbral de 3 %: 2 % × 1.000.000 = 20.000.
        Estimate("HIGH_MATERIAL_LOSS", ImpactCategory.OperationalWaste, Ctx.Build(
            financial: new FinancialSnapshot { MaterialLoss = 50_000m, Cogs = 1_000_000m, Revenue = 1_500_000m })).EstimatedLoss.ShouldBe(20_000m);

    [Fact]
    public void EnergySpike_IsExcessEnergyOverBaseYear()
    {
        // Energía de 5 % a 6 % de los ingresos: 1 % × 1.000.000 = 10.000; el ratio subió 20 %.
        var impact = Estimate("ENERGY_COST_SPIKE", ImpactCategory.OperationalWaste, Ctx.Build(
            K((KpiCode.EnergyCostRatio, 0.06m)), K((KpiCode.EnergyCostRatio, 0.05m)),
            new FinancialSnapshot { Revenue = 1_000_000m }));

        impact.EstimatedLoss.ShouldBe(10_000m);
        impact.DeteriorationMagnitude.ShouldBe(20m);
    }

    // --- Casos límite ---

    [Theory]
    [InlineData("LOW_LIQUIDITY", KpiCode.CurrentRatio, 0.8, 20)]   // mínimo 1,0 → 20 % por debajo
    [InlineData("HIGH_LEVERAGE", KpiCode.DebtToEquity, 4.5, 50)]   // máximo 3,0 → 50 % por encima
    public void FinancialAlerts_AreNotQuantified_ButKeepTheirMagnitude(string code, KpiCode kpi, double value, double magnitude)
    {
        var impact = Estimate(code, ImpactCategory.FinancialAlert, Ctx.Build(K((kpi, (decimal)value))));

        impact.EstimatedLoss.ShouldBeNull();
        impact.Reason!.ShouldContain("Alerta financiera");
        impact.DeteriorationMagnitude.ShouldBe((decimal)magnitude);
    }

    [Fact]
    public void MissingData_ReturnsNullWithReason()
    {
        var noPrevious = Estimate("EBITDA_DECLINE", ImpactCategory.MarginLoss, Ctx.Build(K((KpiCode.EbitdaMargin, 0.1m))));
        noPrevious.EstimatedLoss.ShouldBeNull();
        noPrevious.Reason.ShouldNotBeNullOrEmpty();

        var noRevenue = Estimate("EBITDA_DECLINE", ImpactCategory.MarginLoss, Ctx.Build(
            K((KpiCode.EbitdaMargin, 0.1m)), K((KpiCode.EbitdaMargin, 0.2m))));
        noRevenue.EstimatedLoss.ShouldBeNull();
        noRevenue.DeteriorationMagnitude.ShouldBe(50m);

        Estimate("UNKNOWN_RULE", ImpactCategory.ExcessCost, Ctx.Build()).Reason!.ShouldContain("UNKNOWN_RULE");
    }

    [Fact]
    public void Formulas_NeverReturnNegatives()
    {
        ImpactEngine.MarginLoss(0.10m, 0.20m, 1_000_000m).ShouldBe(0m);
        ImpactEngine.ExcessCost(2m, 10m, 1_000_000m).ShouldBe(0m);
        ImpactEngine.ExcessOverReference(0.02m, 0.03m, 1_000_000m).ShouldBe(0m);
        ImpactEngine.ProductivityLoss(9m, 10m, 5m).ShouldBe(0m);
        ImpactEngine.LostContribution(900m, 1000m, 0.3m).ShouldBe(0m);
    }

    [Fact]
    public void Sentence_FormatsTheLossInTheCompanyCurrency()
    {
        var impact = new ImpactEstimate(1_234_567.4m, 2.5m, 10m, Severity.High, "f", null, []);

        ImpactEngine.Sentence(impact, "DOP").ShouldBe("Impacto estimado: RD$ 1.234.567 en el año, equivalente a 2,5 pp de margen.");
        ImpactEngine.Sentence(impact, "USD")!.ShouldContain("US$ 1.234.567");
        ImpactEngine.Sentence(impact with { EstimatedLoss = null }, "DOP").ShouldBeNull();
        ImpactEngine.Sentence(impact with { EstimatedLoss = 0m }, "DOP").ShouldBeNull();
    }

    [Fact]
    public void ToJson_ContainsEveryField()
    {
        var json = new ImpactEstimate(100m, 1m, 5m, Severity.Low, "f", null, new() { ["x"] = 1 }).ToJson();

        json["estimatedLoss"]!.GetValue<decimal>().ShouldBe(100m);
        json["severityLevel"]!.GetValue<string>().ShouldBe("Low");
        json["inputs"]!["x"]!.GetValue<int>().ShouldBe(1);
    }
}
