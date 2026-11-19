using System.Text.Json.Nodes;
using Opsynex.Domain.Engines.Diagnosis;
using Opsynex.Domain.Engines.Kpis;
using Opsynex.Domain.Enums;

namespace Opsynex.Domain.Engines.Impact;

/// <summary>
/// Impacto económico estimado de un problema (sección 12).
/// </summary>
/// <param name="EstimatedLoss">Pérdida anual estimada en la moneda de la empresa (≥ 0), o <c>null</c> si no se puede calcular.</param>
/// <param name="AffectedMarginPp">Puntos de margen que representa la pérdida (pérdida / ingresos × 100).</param>
/// <param name="DeteriorationMagnitude">Magnitud del deterioro del indicador base, en %.</param>
/// <param name="SeverityLevel">Severidad del problema.</param>
/// <param name="Formula">Fórmula aplicada, legible.</param>
/// <param name="Reason">Motivo cuando no se puede calcular la pérdida.</param>
/// <param name="Inputs">Valores usados en el cálculo.</param>
public sealed record ImpactEstimate(
    decimal? EstimatedLoss,
    decimal? AffectedMarginPp,
    decimal? DeteriorationMagnitude,
    Severity SeverityLevel,
    string Formula,
    string? Reason,
    JsonObject Inputs)
{
    /// <summary>Objeto que se guarda en <c>Problem.Evidence["impact"]</c>.</summary>
    public JsonObject ToJson() => new()
    {
        ["estimatedLoss"] = EstimatedLoss,
        ["affectedMarginPp"] = AffectedMarginPp,
        ["deteriorationMagnitude"] = DeteriorationMagnitude,
        ["severityLevel"] = SeverityLevel.ToString(),
        ["formula"] = Formula,
        ["reason"] = Reason,
        ["inputs"] = Inputs.DeepClone(),
    };
}

/// <summary>
/// Motor de impacto financiero (secciones 8.5 y 12): estima la pérdida económica de cada problema
/// según su categoría. Clase pura: no depende de EF ni de HTTP.
/// <list type="bullet">
/// <item><b>MarginLoss</b>: (margen anterior − margen actual) × ingresos actuales; contra un benchmark,
/// (benchmark − margen) × ingresos; caída de ventas: ingresos perdidos × margen bruto anterior.</item>
/// <item><b>ExcessCost</b>: (crecimiento del costo − crecimiento de los ingresos) × costo del año anterior;
/// contra un benchmark, (ratio − benchmark) × ingresos.</item>
/// <item><b>ProductivityLoss</b>: (ingresos/empleado anterior − actual) × empleados actuales.</item>
/// <item><b>OperationalWaste</b>: pérdida de materiales sobre el umbral × costo de ventas, o exceso de
/// energía sobre el año base: (ratio actual − ratio anterior) × ingresos actuales.</item>
/// <item><b>FinancialAlert</b>: no se cuantifica como pérdida (liquidez y endeudamiento).</item>
/// </list>
/// </summary>
public static class ImpactEngine
{
    /// <summary>Estima el impacto de un problema detectado.</summary>
    public static ImpactEstimate Estimate(DetectedProblem problem, DiagnosisContext context)
    {
        ArgumentNullException.ThrowIfNull(problem);
        return Estimate(problem.Code, problem.Rule.ImpactCategory, problem.Severity, context);
    }

    /// <summary>Estima el impacto de un problema por el código de su regla.</summary>
    public static ImpactEstimate Estimate(string code, ImpactCategory category, Severity severity, DiagnosisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var c = new Calc(severity, context);
        return code switch
        {
            "EBITDA_DECLINE" => c.MarginDecline(KpiCode.EbitdaMargin),
            "GROSS_MARGIN_DECLINE" => c.MarginDecline(KpiCode.GrossMargin),
            "LOW_EBITDA_MARGIN" => c.MarginBelowBenchmark(code),
            "REVENUE_DECLINE" => c.RevenueDecline(),
            "OVERHEAD_OUTPACES_REVENUE" => c.CostOutpacesRevenue(s => s.OverheadCost, "gasto general"),
            "LABOR_COST_OUTPACES_REVENUE" => c.CostOutpacesRevenue(s => s.LaborCost, "costo de personal"),
            "COST_GROWTH_FLAT_REVENUE" => c.CostOutpacesRevenue(s => s.TotalCost, "costo total"),
            "HIGH_OPEX_RATIO" => c.RatioAboveBenchmark(code),
            "PRODUCTIVITY_DECLINE" => c.ProductivityDecline(),
            "HIGH_MATERIAL_LOSS" => c.MaterialLoss(code),
            "ENERGY_COST_SPIKE" => c.EnergySpike(),
            "LOW_LIQUIDITY" => c.Alert(KpiCode.CurrentRatio, code, higherIsBetter: true),
            "HIGH_LEVERAGE" => c.Alert(KpiCode.DebtToEquity, code, higherIsBetter: false),
            _ => category == ImpactCategory.FinancialAlert
                ? Unquantified(severity, "—", "Alerta financiera: no se cuantifica como pérdida.", [])
                : Unquantified(severity, "—", $"No hay fórmula de impacto para la regla {code}.", []),
        };
    }

    /// <summary>Frase con el impacto para completar la recomendación, o <c>null</c> si no hay pérdida cuantificada.</summary>
    public static string? Sentence(ImpactEstimate estimate, string? currency)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        if (estimate.EstimatedLoss is not { } loss || loss <= 0)
        {
            return null;
        }

        var margin = estimate.AffectedMarginPp is { } pp && pp > 0 ? $", equivalente a {Fmt.Times(pp)} pp de margen" : string.Empty;
        return $"Impacto estimado: {Fmt.Money(loss, currency)} en el año{margin}.";
    }

    // --- Fórmulas por categoría (públicas para probarlas por separado). Nunca devuelven negativos. ---

    /// <summary>MarginLoss: (margen anterior − margen actual) × ingresos actuales.</summary>
    public static decimal MarginLoss(decimal previousMargin, decimal currentMargin, decimal revenue) =>
        Money((previousMargin - currentMargin) * revenue);

    /// <summary>ExcessCost: (crecimiento del costo − crecimiento de los ingresos) en % × costo del año anterior.</summary>
    public static decimal ExcessCost(decimal costGrowthPercent, decimal revenueGrowthPercent, decimal previousCost) =>
        Money((costGrowthPercent - revenueGrowthPercent) / 100m * previousCost);

    /// <summary>Exceso de un ratio sobre su referencia × monto base (ExcessCost u OperationalWaste contra un benchmark).</summary>
    public static decimal ExcessOverReference(decimal ratio, decimal reference, decimal baseAmount) =>
        Money((ratio - reference) * baseAmount);

    /// <summary>ProductivityLoss: (ingresos/empleado anterior − actual) × empleados actuales.</summary>
    public static decimal ProductivityLoss(decimal previousRevenuePerEmployee, decimal currentRevenuePerEmployee, decimal employees) =>
        Money((previousRevenuePerEmployee - currentRevenuePerEmployee) * employees);

    /// <summary>MarginLoss por caída de ventas: ingresos perdidos × margen del año anterior.</summary>
    public static decimal LostContribution(decimal previousRevenue, decimal currentRevenue, decimal previousMargin) =>
        Money((previousRevenue - currentRevenue) * previousMargin);

    private static decimal Money(decimal value) => decimal.Round(Math.Max(0m, value), 2);

    private static decimal? Round2(decimal? value) => value is { } v ? decimal.Round(v, 2) : null;

    private static ImpactEstimate Unquantified(Severity severity, string formula, string reason, JsonObject inputs, decimal? magnitude = null) =>
        new(null, null, Round2(magnitude), severity, formula, reason, inputs);

    /// <summary>Cálculos sobre el contexto de un año.</summary>
    private sealed class Calc(Severity severity, DiagnosisContext ctx)
    {
        private const string NoPrevious = "Falta el año anterior para comparar.";

        private FinancialSnapshot Cur => ctx.Financial;

        private FinancialSnapshot? Prev => ctx.PreviousFinancial;

        public ImpactEstimate MarginDecline(KpiCode kpi)
        {
            const string formula = "(margen anterior − margen actual) × ingresos actuales";
            var inputs = new JsonObject { ["kpi"] = kpi.ToString(), ["previousMargin"] = ctx.PreviousKpi(kpi), ["currentMargin"] = ctx.Kpi(kpi), ["revenue"] = Cur.Revenue };
            if (ctx.PreviousKpi(kpi) is not { } p || ctx.Kpi(kpi) is not { } c)
            {
                return Unquantified(severity, formula, NoPrevious, inputs);
            }

            var magnitude = p > 0 ? (p - c) / p * 100m : (decimal?)null;
            return Cur.Revenue is { } revenue
                ? Result(MarginLoss(p, c, revenue), formula, inputs, magnitude)
                : Unquantified(severity, formula, "Faltan los ingresos del año.", inputs, magnitude);
        }

        public ImpactEstimate MarginBelowBenchmark(string code)
        {
            const string formula = "(margen de referencia − margen actual) × ingresos actuales";
            var benchmark = ctx.Settings.For(code).ReferenceFor(ctx.Industry);
            var inputs = new JsonObject { ["kpi"] = KpiCode.EbitdaMargin.ToString(), ["benchmark"] = benchmark, ["currentMargin"] = ctx.Kpi(KpiCode.EbitdaMargin), ["revenue"] = Cur.Revenue };
            if (benchmark is not { } b || ctx.Kpi(KpiCode.EbitdaMargin) is not { } c || Cur.Revenue is not { } revenue)
            {
                return Unquantified(severity, formula, "Faltan el margen, los ingresos o la referencia de la industria.", inputs);
            }

            return Result(MarginLoss(b, c, revenue), formula, inputs, b > 0 ? (b - c) / b * 100m : null);
        }

        public ImpactEstimate RevenueDecline()
        {
            const string formula = "(ingresos anteriores − ingresos actuales) × margen bruto anterior";
            var margin = ctx.PreviousKpi(KpiCode.GrossMargin) ?? ctx.PreviousKpi(KpiCode.EbitdaMargin);
            var inputs = new JsonObject { ["previousRevenue"] = Prev?.Revenue, ["currentRevenue"] = Cur.Revenue, ["previousMargin"] = margin };
            if (Prev?.Revenue is not { } p || Cur.Revenue is not { } c || margin is not { } m || p <= 0)
            {
                return Unquantified(severity, formula, "Faltan los ingresos o el margen del año anterior.", inputs);
            }

            return Result(LostContribution(p, c, m), formula, inputs, (p - c) / p * 100m);
        }

        public ImpactEstimate CostOutpacesRevenue(Func<FinancialSnapshot, decimal?> cost, string costName)
        {
            var formula = $"(crecimiento del {costName} − crecimiento de los ingresos) × {costName} del año anterior";
            var previousCost = Prev is null ? null : cost(Prev);
            var costGrowth = DiagnosisContext.Growth(previousCost, cost(Cur));
            var revenueGrowth = DiagnosisContext.Growth(Prev?.Revenue, Cur.Revenue);
            var inputs = new JsonObject
            {
                ["previousCost"] = previousCost,
                ["currentCost"] = cost(Cur),
                ["costGrowthPercent"] = Round2(costGrowth),
                ["revenueGrowthPercent"] = Round2(revenueGrowth),
            };
            if (previousCost is not { } pc || costGrowth is not { } cg || revenueGrowth is not { } rg)
            {
                return Unquantified(severity, formula, NoPrevious, inputs);
            }

            return Result(ExcessCost(cg, rg, pc), formula, inputs, cg - rg);
        }

        public ImpactEstimate RatioAboveBenchmark(string code)
        {
            const string formula = "(gastos operacionales / ingresos − referencia) × ingresos actuales";
            var benchmark = ctx.Settings.For(code).ReferenceFor(ctx.Industry);
            var ratio = ctx.Kpi(KpiCode.OperatingCostRatio);
            var inputs = new JsonObject { ["kpi"] = KpiCode.OperatingCostRatio.ToString(), ["ratio"] = ratio, ["benchmark"] = benchmark, ["revenue"] = Cur.Revenue };
            if (ratio is not { } r || benchmark is not { } b || Cur.Revenue is not { } revenue)
            {
                return Unquantified(severity, formula, "Faltan el ratio, los ingresos o la referencia de la industria.", inputs);
            }

            return Result(ExcessOverReference(r, b, revenue), formula, inputs, b > 0 ? (r - b) / b * 100m : null);
        }

        public ImpactEstimate ProductivityDecline()
        {
            const string formula = "(ingresos por empleado anteriores − actuales) × empleados actuales";
            var p = ctx.PreviousKpi(KpiCode.RevenuePerEmployee);
            var c = ctx.Kpi(KpiCode.RevenuePerEmployee);
            var employees = Cur.EmployeeCount ?? (Cur.Revenue is { } rev && c is > 0 ? (int)decimal.Round(rev / c.Value) : null);
            var inputs = new JsonObject { ["previousRevenuePerEmployee"] = p, ["currentRevenuePerEmployee"] = c, ["employees"] = employees };
            if (p is not { } pv || c is not { } cv || employees is not { } e || pv <= 0)
            {
                return Unquantified(severity, formula, "Faltan los ingresos por empleado o la cantidad de empleados.", inputs);
            }

            return Result(ProductivityLoss(pv, cv, e), formula, inputs, (pv - cv) / pv * 100m);
        }

        public ImpactEstimate MaterialLoss(string code)
        {
            const string formula = "(pérdida de materiales / costo de ventas − umbral) × costo de ventas";
            var threshold = ctx.Settings.For(code).ReferenceFor(ctx.Industry);
            var ratio = KpiEngine.SafeDivide(Cur.MaterialLoss, Cur.Cogs);
            var inputs = new JsonObject { ["materialLoss"] = Cur.MaterialLoss, ["cogs"] = Cur.Cogs, ["ratio"] = Round4(ratio), ["threshold"] = threshold };
            if (ratio is not { } r || threshold is not { } t || Cur.Cogs is not { } cogs)
            {
                return Unquantified(severity, formula, "Faltan la pérdida de materiales, el costo de ventas o el umbral.", inputs);
            }

            return Result(ExcessOverReference(r, t, cogs), formula, inputs, t > 0 ? (r - t) / t * 100m : null);
        }

        public ImpactEstimate EnergySpike()
        {
            const string formula = "(energía / ingresos actual − energía / ingresos del año base) × ingresos actuales";
            var p = ctx.PreviousKpi(KpiCode.EnergyCostRatio);
            var c = ctx.Kpi(KpiCode.EnergyCostRatio);
            var inputs = new JsonObject { ["previousRatio"] = p, ["currentRatio"] = c, ["revenue"] = Cur.Revenue, ["currentEnergyCost"] = Cur.EnergyCost };
            if (p is not { } pv || c is not { } cv || Cur.Revenue is not { } revenue || pv <= 0)
            {
                return Unquantified(severity, formula, NoPrevious, inputs);
            }

            return Result(ExcessOverReference(cv, pv, revenue), formula, inputs, (cv - pv) / pv * 100m);
        }

        public ImpactEstimate Alert(KpiCode kpi, string code, bool higherIsBetter)
        {
            var reference = ctx.Settings.For(code).ReferenceFor(ctx.Industry);
            var value = ctx.Kpi(kpi);
            decimal? magnitude = value is { } v && reference is { } r && r > 0 ? (higherIsBetter ? r - v : v - r) / r * 100m : null;
            return Unquantified(
                severity,
                "—",
                "Alerta financiera: indica un riesgo, no una pérdida operativa, y no se cuantifica.",
                new JsonObject { ["kpi"] = kpi.ToString(), ["current"] = value, ["reference"] = reference },
                magnitude);
        }

        private ImpactEstimate Result(decimal loss, string formula, JsonObject inputs, decimal? magnitude)
        {
            decimal? affected = Cur.Revenue is > 0 ? decimal.Round(loss / Cur.Revenue.Value * 100m, 2) : null;
            return new ImpactEstimate(loss, affected, Round2(magnitude), severity, formula, null, inputs);
        }

        private static decimal? Round4(decimal? value) => value is { } v ? decimal.Round(v, 4) : null;
    }
}
