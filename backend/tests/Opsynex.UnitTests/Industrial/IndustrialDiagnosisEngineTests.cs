using Opsynex.Domain.Engines.Industrial;
using Opsynex.Domain.Enums;

namespace Opsynex.UnitTests.Industrial;

/// <summary>Reglas industriales (§6) e impacto (§7): disparan / no disparan, severidad, impacto sin doble conteo.</summary>
public sealed class IndustrialDiagnosisEngineTests
{
    private static readonly DateOnly From = new(2024, 10, 1);
    private static readonly DateOnly To = new(2024, 12, 31);

    private static IndustrialAggregate Machine(
        string id,
        decimal? oee = 0.85m,
        decimal? scrap = 0.02m,
        decimal? downtime = 0.05m,
        decimal? costPerUnit = null,
        decimal? standardCost = null,
        decimal? downtimeCost = 1_000m,
        decimal? scrapCost = 200m,
        decimal? speedCost = 300m,
        decimal produced = 1_000m,
        decimal? actualCost = null)
    {
        var costs = actualCost is { } c ? new CostBreakdown(0m, c, 0m, 0m) : null;
        var sums = new IndustrialSums(10_000m, 500m, 1_100m, produced, produced, 20, 8_000m, 9_500m, 10_000m,
            costs, produced, standardCost is { } s ? s * produced : null, produced);
        return new IndustrialAggregate(2024, null, IndustrialLevel.Machine, id, "L1", sums,
            new IndustrialKpis(0.95m, 0.9m, 0.98m, oee, scrap, downtime, 0.8m, costPerUnit, standardCost),
            new IndustrialLosses(downtimeCost, scrapCost, speedCost), 10m, 5m);
    }

    private static IReadOnlyList<IndustrialProblem> Evaluate(IndustrialSettings? settings = null, params IndustrialAggregate[] machines) =>
        IndustrialDiagnosisEngine.Evaluate(machines, null, From, To, settings ?? new IndustrialSettings(), "DOP");

    [Fact]
    public void HealthyMachines_HaveNoProblems() =>
        Evaluate(null, Machine("M1"), Machine("M2", costPerUnit: 10.5m, standardCost: 10m)).ShouldBeEmpty();

    [Theory]
    [InlineData(0.59, true)]
    [InlineData(0.60, false)] // OEE < 0,60
    public void LowOee_IsHighWhenOeeBelow60(double oee, bool fires)
    {
        var problems = Evaluate(null, Machine("M1", oee: (decimal)oee));

        problems.Any(p => p.Code == IndustrialDiagnosisEngine.LowOee).ShouldBe(fires);
        if (fires)
        {
            var problem = problems.Single();
            problem.Severity.ShouldBe(Severity.High);
            problem.Category.ShouldBe(ProblemCategory.Operations);
            problem.ImpactCategory.ShouldBe(ImpactCategory.ProductivityLoss);
            problem.Description.ShouldContain("M1 (59 %)");
        }
    }

    [Theory]
    [InlineData(0.051, true)]
    [InlineData(0.05, false)] // scrap > 0,05
    public void HighScrap_IsMediumWhenScrapAbove5(double scrap, bool fires)
    {
        var problems = Evaluate(null, Machine("M1", scrap: (decimal)scrap));

        problems.Any(p => p.Code == IndustrialDiagnosisEngine.HighScrap).ShouldBe(fires);
        if (fires)
        {
            var problem = problems.Single();
            problem.Severity.ShouldBe(Severity.Medium);
            problem.ImpactCategory.ShouldBe(ImpactCategory.OperationalWaste);
            problem.Impact.EstimatedLoss.ShouldBe(200m); // (total − buenas) × costo unitario
            problem.Impact.Formula.ShouldBe("(unidades totales − unidades buenas) × costo unitario");
        }
    }

    [Theory]
    [InlineData(0.16, true)]
    [InlineData(0.15, false)] // paradas / planificado > 0,15
    public void HighDowntime_IsHighWhenAboveThreshold(double ratio, bool fires)
    {
        var problems = Evaluate(null, Machine("M1", downtime: (decimal)ratio), Machine("M2", downtime: (decimal)ratio));

        problems.Any(p => p.Code == IndustrialDiagnosisEngine.HighDowntime).ShouldBe(fires);
        if (fires)
        {
            var problem = problems.Single();
            problem.Severity.ShouldBe(Severity.High);
            problem.Impact.EstimatedLoss.ShouldBe(2_000m); // Σ minutos de parada × costo por minuto de M1 y M2
            problem.Impact.DeteriorationMagnitude.ShouldBe(6.67m); // 16 % frente a 15 %
            problem.Evidence["machinesAffected"]!.GetValue<string>().ShouldBe("M1, M2");
            problem.Evidence["kpi"]!.GetValue<string>().ShouldBe("DowntimeRatio");
        }
    }

    [Fact]
    public void LowOee_AddsSpeedLoss_AndOnlyTheDowntimeAndScrapNotCountedElsewhere()
    {
        // Solo OEE bajo: la pérdida completa = velocidad + paradas + scrap.
        Evaluate(null, Machine("M1", oee: 0.5m)).Single().Impact.EstimatedLoss.ShouldBe(1_500m);

        // OEE bajo + paradas altas + scrap alto: cada pérdida se cuenta una sola vez.
        var problems = Evaluate(null, Machine("M1", oee: 0.5m, downtime: 0.3m, scrap: 0.08m));
        problems.Select(p => p.Code).ShouldBe([IndustrialDiagnosisEngine.HighDowntime, IndustrialDiagnosisEngine.HighScrap, IndustrialDiagnosisEngine.LowOee]);
        problems.Single(p => p.Code == IndustrialDiagnosisEngine.LowOee).Impact.EstimatedLoss.ShouldBe(300m);
        problems.Sum(p => p.Impact.EstimatedLoss!.Value).ShouldBe(1_500m);
    }

    [Fact]
    public void LowOee_RecommendsTheWeakestComponent()
    {
        var problem = Evaluate(null, Machine("M1", oee: 0.5m)).Single();

        problem.Recommendation.ShouldContain("(rendimiento)"); // 0,90 frente a 0,95 y 0,98
        problem.Description.ShouldContain("rendimiento 90 %");
    }

    [Fact]
    public void CostAboveStandard_IsTheExcessCostNotAlreadyAttributed()
    {
        // 12 frente a 10 (+20 %): exceso = 12.000 − 10.000 = 2.000.
        var alone = Evaluate(null, Machine("M1", costPerUnit: 12m, standardCost: 10m, actualCost: 12_000m)).Single();
        alone.Code.ShouldBe(IndustrialDiagnosisEngine.CostAboveStandard);
        alone.Severity.ShouldBe(Severity.Medium);
        alone.Category.ShouldBe(ProblemCategory.CostEfficiency);
        alone.ImpactCategory.ShouldBe(ImpactCategory.ExcessCost);
        alone.Impact.EstimatedLoss.ShouldBe(2_000m);
        alone.Description.ShouldContain("RD$ 12,00 frente a RD$ 10,00");

        // Con scrap alto en la misma máquina, su costo (200) ya está en HIGH_SCRAP.
        var problems = Evaluate(null, Machine("M1", scrap: 0.08m, costPerUnit: 12m, standardCost: 10m, actualCost: 12_000m));
        problems.Single(p => p.Code == IndustrialDiagnosisEngine.CostAboveStandard).Impact.EstimatedLoss.ShouldBe(1_800m);
    }

    [Theory]
    [InlineData(10.9, false)] // tolerancia del 10 %
    [InlineData(11.1, true)]
    public void CostAboveStandard_RespectsTheTolerance(double costPerUnit, bool fires) =>
        Evaluate(null, Machine("M1", costPerUnit: (decimal)costPerUnit, standardCost: 10m)).Any().ShouldBe(fires);

    [Fact]
    public void ThresholdsAndSeverities_AreConfigurable()
    {
        var settings = new IndustrialSettings { LowOeeThreshold = 0.9m, HighScrapThreshold = 0.01m };
        settings.Severities[IndustrialDiagnosisEngine.HighScrap] = Severity.High;

        var problems = Evaluate(settings, Machine("M1"));

        problems.Select(p => p.Code).ShouldBe([IndustrialDiagnosisEngine.HighScrap, IndustrialDiagnosisEngine.LowOee]);
        problems[0].Severity.ShouldBe(Severity.High);
    }

    [Fact]
    public void MissingCosts_DetectTheProblem_WithoutQuantifyingIt()
    {
        var problem = Evaluate(null, Machine("M1", downtime: 0.3m, downtimeCost: null)).Single();

        problem.Impact.EstimatedLoss.ShouldBeNull();
        problem.Impact.Reason!.ShouldContain("costo por minuto");
        IndustrialDiagnosisEngine.Sentence(problem.Impact, "DOP", "oct 2024").ShouldBeNull();
    }

    [Fact]
    public void ImpactSentence_NamesTheAnalyzedPeriod()
    {
        var problem = Evaluate(null, Machine("M1", downtime: 0.3m, downtimeCost: 1_234_567.4m)).Single();

        IndustrialDiagnosisEngine.Sentence(problem.Impact, "DOP", IndustrialDiagnosisEngine.PeriodLabel(From, To))
            .ShouldBe("Impacto estimado: RD$ 1.234.567 en el período analizado (oct–dic 2024).");
        problem.Evidence["impact"].ShouldBeNull(); // la evidencia del impacto la agrega el servicio
        problem.Impact.ToJson()["inputs"]!["machines"]!["M1"]!["costPerMinute"]!.GetValue<decimal>().ShouldBe(10m);
    }

    [Theory]
    [InlineData("2024-10-01", "2024-10-31", "oct 2024")]
    [InlineData("2024-10-01", "2024-12-31", "oct–dic 2024")]
    [InlineData("2024-11-01", "2025-02-28", "nov 2024–feb 2025")]
    public void PeriodLabel_IsShortAndInSpanish(string from, string to, string expected) =>
        IndustrialDiagnosisEngine.PeriodLabel(DateOnly.Parse(from, System.Globalization.CultureInfo.InvariantCulture), DateOnly.Parse(to, System.Globalization.CultureInfo.InvariantCulture))
            .ShouldBe(expected);

    [Fact]
    public void Codes_ListTheFourIndustrialRules() =>
        IndustrialDiagnosisEngine.Codes.Order().ShouldBe(["COST_PER_UNIT_ABOVE_STANDARD", "HIGH_DOWNTIME", "HIGH_SCRAP", "LOW_OEE"]);
}
