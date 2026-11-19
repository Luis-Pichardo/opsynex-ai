using System.Text.Json.Nodes;
using Opsynex.Domain.Engines.Kpis;
using Opsynex.Domain.Enums;

namespace Opsynex.Domain.Engines.Diagnosis.Rules;

/// <summary>Regla por caída de un margen año contra año (magnitud = caída en puntos porcentuales).</summary>
public abstract class MarginDeclineRule(KpiCode kpi) : IDiagnosticRule
{
    public abstract string Code { get; }

    public abstract string Title { get; }

    public ProblemCategory Category => ProblemCategory.Profitability;

    public ImpactCategory ImpactCategory => ImpactCategory.MarginLoss;

    protected abstract string Describe(string previous, string current, string change, int previousYear, int year);

    protected abstract string Recommend(string previous, string current, string change);

    public RuleResult? Evaluate(DiagnosisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.HasPrevious || context.PreviousKpi(kpi) is not { } previous || context.Kpi(kpi) is not { } current)
        {
            return null;
        }

        var dropPp = (previous - current) * 100m;
        if (context.Settings.For(Code).Classify(dropPp) is not { } severity)
        {
            return null;
        }

        var evidence = new JsonObject
        {
            ["kpi"] = kpi.ToString(),
            ["year"] = context.Year,
            ["previousYear"] = context.PreviousYear,
            ["previous"] = previous,
            ["current"] = current,
            ["changePp"] = Fmt.R(-dropPp, 2),
        };
        return new RuleResult(
            severity,
            Describe(Fmt.Pct(previous), Fmt.Pct(current), Fmt.Pp(-dropPp), context.PreviousYear!.Value, context.Year),
            Recommend(Fmt.Pct(previous), Fmt.Pct(current), Fmt.Pp(-dropPp)),
            evidence,
            kpi);
    }
}

/// <summary>EBITDA_DECLINE: disminuye el margen EBITDA → deterioro de rentabilidad.</summary>
public sealed class EbitdaDeclineRule() : MarginDeclineRule(KpiCode.EbitdaMargin)
{
    public override string Code => "EBITDA_DECLINE";

    public override string Title => "Deterioro de rentabilidad";

    protected override string Describe(string previous, string current, string change, int previousYear, int year) =>
        $"El margen EBITDA bajó de {previous} en {previousYear} a {current} en {year} ({change}).";

    protected override string Recommend(string previous, string current, string change) =>
        $"Para recuperar el margen EBITDA ({current} frente a {previous}, {change}), revisa qué costos crecieron más que los " +
        "ingresos (costo de ventas, personal, gastos generales) y prioriza las acciones de precio, mezcla de productos y " +
        "control de gastos con mayor efecto sobre el margen.";
}

/// <summary>GROSS_MARGIN_DECLINE: se deteriora el margen bruto → problema de absorción de costos.</summary>
public sealed class GrossMarginDeclineRule() : MarginDeclineRule(KpiCode.GrossMargin)
{
    public override string Code => "GROSS_MARGIN_DECLINE";

    public override string Title => "Problema de absorción de costos";

    protected override string Describe(string previous, string current, string change, int previousYear, int year) =>
        $"El margen bruto bajó de {previous} en {previousYear} a {current} en {year} ({change}): el costo de ventas crece más que los ingresos.";

    protected override string Recommend(string previous, string current, string change) =>
        $"El margen bruto pasó de {previous} a {current} ({change}): analiza el costo de producción (materias primas, labor " +
        "directa, gastos de fabricación) y los precios de venta; negocia con proveedores y revisa la eficiencia de la planta.";
}

/// <summary>Regla por un costo que crece más que los ingresos (magnitud = diferencia en pp).</summary>
public abstract class CostOutpacesRevenueRule : IDiagnosticRule
{
    public abstract string Code { get; }

    public abstract string Title { get; }

    public ProblemCategory Category => ProblemCategory.CostEfficiency;

    public ImpactCategory ImpactCategory => ImpactCategory.ExcessCost;

    protected abstract string CostName { get; }

    protected abstract KpiCode RelatedKpi { get; }

    protected abstract decimal? Cost(FinancialSnapshot snapshot);

    protected abstract string Recommendation { get; }

    public RuleResult? Evaluate(DiagnosisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.HasPrevious)
        {
            return null;
        }

        var costGrowth = DiagnosisContext.Growth(Cost(context.PreviousFinancial!), Cost(context.Financial));
        var revenueGrowth = DiagnosisContext.Growth(context.PreviousFinancial!.Revenue, context.Financial.Revenue);
        if (costGrowth is not { } cg || revenueGrowth is not { } rg)
        {
            return null;
        }

        var gap = cg - rg;
        if (context.Settings.For(Code).Classify(gap) is not { } severity)
        {
            return null;
        }

        var evidence = new JsonObject
        {
            ["kpi"] = RelatedKpi.ToString(),
            ["year"] = context.Year,
            ["previousYear"] = context.PreviousYear,
            ["costGrowthPercent"] = Fmt.R(cg, 2),
            ["revenueGrowthPercent"] = Fmt.R(rg, 2),
            ["gapPp"] = Fmt.R(gap, 2),
            ["previousCost"] = Cost(context.PreviousFinancial),
            ["currentCost"] = Cost(context.Financial),
            ["previousRevenue"] = context.PreviousFinancial.Revenue,
            ["currentRevenue"] = context.Financial.Revenue,
        };
        return new RuleResult(
            severity,
            $"{CostName} creció {Fmt.Percent(cg)} entre {context.PreviousYear} y {context.Year}, más que los ingresos ({Fmt.Percent(rg)}).",
            $"{CostName} creció {Fmt.Percent(cg)} frente a {Fmt.Percent(rg)} de los ingresos; {Recommendation}",
            evidence,
            RelatedKpi);
    }
}

/// <summary>OVERHEAD_OUTPACES_REVENUE: el overhead crece más que el revenue → ineficiencia administrativa.</summary>
public sealed class OverheadOutpacesRevenueRule : CostOutpacesRevenueRule
{
    public override string Code => "OVERHEAD_OUTPACES_REVENUE";

    public override string Title => "Ineficiencia administrativa";

    protected override string CostName => "El gasto general y administrativo";

    protected override KpiCode RelatedKpi => KpiCode.OverheadRatio;

    protected override decimal? Cost(FinancialSnapshot snapshot) => snapshot.OverheadCost;

    protected override string Recommendation =>
        "revisa la estructura administrativa (honorarios, alquileres, seguros, servicios) y fija un presupuesto que crezca " +
        "por debajo de las ventas; elimina gastos que no aportan a la operación.";
}

/// <summary>LABOR_COST_OUTPACES_REVENUE: el costo laboral crece más que los ingresos.</summary>
public sealed class LaborCostOutpacesRevenueRule : CostOutpacesRevenueRule
{
    public override string Code => "LABOR_COST_OUTPACES_REVENUE";

    public override string Title => "Ineficiencia en costos de personal";

    protected override string CostName => "El costo de personal";

    protected override KpiCode RelatedKpi => KpiCode.LaborCostRatio;

    protected override decimal? Cost(FinancialSnapshot snapshot) => snapshot.LaborCost;

    protected override string Recommendation =>
        "compara la dotación y los salarios con el volumen de producción y ventas; revisa horas extras, bonificaciones y " +
        "la productividad por área antes de nuevas contrataciones.";
}

/// <summary>COST_GROWTH_FLAT_REVENUE: los costos totales suben con ingresos estables → ineficiencia operacional.</summary>
public sealed class CostGrowthFlatRevenueRule : IDiagnosticRule
{
    public string Code => "COST_GROWTH_FLAT_REVENUE";

    public string Title => "Ineficiencia operacional";

    public ProblemCategory Category => ProblemCategory.CostEfficiency;

    public ImpactCategory ImpactCategory => ImpactCategory.ExcessCost;

    public RuleResult? Evaluate(DiagnosisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.HasPrevious)
        {
            return null;
        }

        var settings = context.Settings.For(Code);
        var flatBand = settings.ReferenceFor(context.Industry) ?? 2m;
        var revenueGrowth = DiagnosisContext.Growth(context.PreviousFinancial!.Revenue, context.Financial.Revenue);
        var costGrowth = DiagnosisContext.Growth(context.PreviousFinancial.TotalCost, context.Financial.TotalCost);
        if (revenueGrowth is not { } rg || costGrowth is not { } cg || Math.Abs(rg) > flatBand
            || settings.Classify(cg) is not { } severity)
        {
            return null;
        }

        return new RuleResult(
            severity,
            $"El costo total creció {Fmt.Percent(cg)} entre {context.PreviousYear} y {context.Year} mientras los ingresos se mantuvieron estables ({Fmt.Percent(rg)}).",
            $"El costo total subió {Fmt.Percent(cg)} con ingresos estables ({Fmt.Percent(rg)}): identifica las partidas que " +
            "aumentaron sin un aumento de ventas (energía, mantenimiento, personal, materiales) y fija metas de reducción por área.",
            new JsonObject
            {
                ["kpi"] = KpiCode.CostEfficiency.ToString(),
                ["year"] = context.Year,
                ["previousYear"] = context.PreviousYear,
                ["costGrowthPercent"] = Fmt.R(cg, 2),
                ["revenueGrowthPercent"] = Fmt.R(rg, 2),
                ["flatBandPercent"] = flatBand,
                ["previousCost"] = context.PreviousFinancial.TotalCost,
                ["currentCost"] = context.Financial.TotalCost,
            },
            KpiCode.CostEfficiency);
    }
}

/// <summary>PRODUCTIVITY_DECLINE: disminuye el revenue per employee → deterioro de productividad.</summary>
public sealed class ProductivityDeclineRule : IDiagnosticRule
{
    public string Code => "PRODUCTIVITY_DECLINE";

    public string Title => "Deterioro de productividad";

    public ProblemCategory Category => ProblemCategory.Productivity;

    public ImpactCategory ImpactCategory => ImpactCategory.ProductivityLoss;

    public RuleResult? Evaluate(DiagnosisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var previous = context.PreviousKpi(KpiCode.RevenuePerEmployee);
        var current = context.Kpi(KpiCode.RevenuePerEmployee);
        if (!context.HasPrevious || previous is not { } p || current is not { } c || p <= 0)
        {
            return null;
        }

        var dropPercent = (p - c) / p * 100m;
        if (context.Settings.For(Code).Classify(dropPercent) is not { } severity)
        {
            return null;
        }

        return new RuleResult(
            severity,
            $"Los ingresos por empleado bajaron {Fmt.Percent(-dropPercent)} entre {context.PreviousYear} y {context.Year}.",
            $"Los ingresos por empleado variaron {Fmt.Percent(-dropPercent)} frente al año anterior: revisa la dotación " +
            "frente al volumen de ventas y producción, la capacidad ociosa y los procesos con más horas por unidad; evalúa " +
            "automatización y capacitación.",
            new JsonObject
            {
                ["kpi"] = KpiCode.RevenuePerEmployee.ToString(),
                ["year"] = context.Year,
                ["previousYear"] = context.PreviousYear,
                ["previous"] = p,
                ["current"] = c,
                ["changePercent"] = Fmt.R(-dropPercent, 2),
            },
            KpiCode.RevenuePerEmployee);
    }
}

/// <summary>REVENUE_DECLINE: los ingresos caen más que el umbral → contracción de ventas.</summary>
public sealed class RevenueDeclineRule : IDiagnosticRule
{
    public string Code => "REVENUE_DECLINE";

    public string Title => "Contracción de ventas";

    public ProblemCategory Category => ProblemCategory.Profitability;

    public ImpactCategory ImpactCategory => ImpactCategory.MarginLoss;

    public RuleResult? Evaluate(DiagnosisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.HasPrevious || DiagnosisContext.Growth(context.PreviousFinancial!.Revenue, context.Financial.Revenue) is not { } growth
            || context.Settings.For(Code).Classify(-growth) is not { } severity)
        {
            return null;
        }

        return new RuleResult(
            severity,
            $"Los ingresos cayeron {Fmt.Percent(growth)} entre {context.PreviousYear} y {context.Year}.",
            $"Con ventas {Fmt.Percent(growth)} frente al año anterior, analiza la caída por cliente, producto y canal; refuerza " +
            "la gestión comercial y ajusta la estructura de costos fijos al nuevo nivel de ventas.",
            new JsonObject
            {
                ["kpi"] = KpiCode.EbitdaMargin.ToString(),
                ["year"] = context.Year,
                ["previousYear"] = context.PreviousYear,
                ["previousRevenue"] = context.PreviousFinancial.Revenue,
                ["currentRevenue"] = context.Financial.Revenue,
                ["revenueGrowthPercent"] = Fmt.R(growth, 2),
            },
            KpiCode.EbitdaMargin);
    }
}

/// <summary>ENERGY_COST_SPIKE: el ratio de energía crece más del umbral (10 %) → consumo energético ineficiente.</summary>
public sealed class EnergyCostSpikeRule : IDiagnosticRule
{
    public string Code => "ENERGY_COST_SPIKE";

    public string Title => "Consumo energético ineficiente";

    public ProblemCategory Category => ProblemCategory.Operations;

    public ImpactCategory ImpactCategory => ImpactCategory.OperationalWaste;

    public RuleResult? Evaluate(DiagnosisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var previous = context.PreviousKpi(KpiCode.EnergyCostRatio);
        var current = context.Kpi(KpiCode.EnergyCostRatio);
        if (!context.HasPrevious || previous is not { } p || current is not { } c || p <= 0)
        {
            return null;
        }

        var increase = (c - p) / p * 100m;
        if (context.Settings.For(Code).Classify(increase) is not { } severity)
        {
            return null;
        }

        return new RuleResult(
            severity,
            $"El costo de energía pasó de {Fmt.Pct(p)} a {Fmt.Pct(c)} de los ingresos entre {context.PreviousYear} y {context.Year} ({Fmt.Percent(increase)}).",
            $"La energía pasó a representar {Fmt.Pct(c)} de los ingresos (antes {Fmt.Pct(p)}): audita el consumo eléctrico " +
            "de la planta (equipos, turnos, factor de potencia), revisa la tarifa contratada y evalúa medidas de eficiencia energética.",
            new JsonObject
            {
                ["kpi"] = KpiCode.EnergyCostRatio.ToString(),
                ["year"] = context.Year,
                ["previousYear"] = context.PreviousYear,
                ["previous"] = p,
                ["current"] = c,
                ["changePercent"] = Fmt.R(increase, 2),
                ["previousEnergyCost"] = context.PreviousFinancial!.EnergyCost,
                ["currentEnergyCost"] = context.Financial.EnergyCost,
            },
            KpiCode.EnergyCostRatio);
    }
}
