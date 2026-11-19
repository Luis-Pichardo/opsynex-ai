using System.Text.Json.Nodes;
using Opsynex.Domain.Engines.Kpis;
using Opsynex.Domain.Enums;

namespace Opsynex.Domain.Engines.Diagnosis.Rules;

/// <summary>LOW_EBITDA_MARGIN: margen EBITDA bajo el benchmark de la industria (magnitud = pp por debajo).</summary>
public sealed class LowEbitdaMarginRule : IDiagnosticRule
{
    public string Code => "LOW_EBITDA_MARGIN";

    public string Title => "Rentabilidad baja";

    public ProblemCategory Category => ProblemCategory.Profitability;

    public ImpactCategory ImpactCategory => ImpactCategory.MarginLoss;

    public RuleResult? Evaluate(DiagnosisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var settings = context.Settings.For(Code);
        if (context.Kpi(KpiCode.EbitdaMargin) is not { } margin || settings.ReferenceFor(context.Industry) is not { } benchmark
            || settings.Classify((benchmark - margin) * 100m) is not { } severity)
        {
            return null;
        }

        return new RuleResult(
            severity,
            $"El margen EBITDA de {context.Year} ({Fmt.Pct(margin)}) está por debajo de la referencia de la industria ({Fmt.Pct(benchmark)}).",
            "Compara precios, costos de producción y gastos con empresas del sector y define un plan para llevar el margen " +
            "a la referencia.",
            new JsonObject
            {
                ["kpi"] = KpiCode.EbitdaMargin.ToString(),
                ["year"] = context.Year,
                ["current"] = margin,
                ["benchmark"] = benchmark,
                ["gapPp"] = Fmt.R((benchmark - margin) * 100m, 2),
            },
            KpiCode.EbitdaMargin);
    }
}

/// <summary>HIGH_OPEX_RATIO: gastos operacionales / ingresos sobre el benchmark (magnitud = pp por encima).</summary>
public sealed class HighOpexRatioRule : IDiagnosticRule
{
    public string Code => "HIGH_OPEX_RATIO";

    public string Title => "Estructura de gastos pesada";

    public ProblemCategory Category => ProblemCategory.CostEfficiency;

    public ImpactCategory ImpactCategory => ImpactCategory.ExcessCost;

    public RuleResult? Evaluate(DiagnosisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var settings = context.Settings.For(Code);
        if (context.Kpi(KpiCode.OperatingCostRatio) is not { } ratio || settings.ReferenceFor(context.Industry) is not { } benchmark
            || settings.Classify((ratio - benchmark) * 100m) is not { } severity)
        {
            return null;
        }

        return new RuleResult(
            severity,
            $"Los gastos operacionales representan {Fmt.Pct(ratio)} de los ingresos en {context.Year}, por encima de la referencia ({Fmt.Pct(benchmark)}).",
            "Revisa los gastos de administración y ventas, renegocia contratos y elimina duplicidades.",
            new JsonObject
            {
                ["kpi"] = KpiCode.OperatingCostRatio.ToString(),
                ["year"] = context.Year,
                ["current"] = ratio,
                ["benchmark"] = benchmark,
                ["gapPp"] = Fmt.R((ratio - benchmark) * 100m, 2),
            },
            KpiCode.OperatingCostRatio);
    }
}

/// <summary>HIGH_MATERIAL_LOSS: pérdida de materiales / COGS sobre el umbral (magnitud = pp por encima).</summary>
public sealed class HighMaterialLossRule : IDiagnosticRule
{
    public string Code => "HIGH_MATERIAL_LOSS";

    public string Title => "Desperdicio de materiales";

    public ProblemCategory Category => ProblemCategory.Operations;

    public ImpactCategory ImpactCategory => ImpactCategory.OperationalWaste;

    public RuleResult? Evaluate(DiagnosisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var settings = context.Settings.For(Code);
        if (KpiEngine.SafeDivide(context.Financial.MaterialLoss, context.Financial.Cogs) is not { } ratio
            || settings.ReferenceFor(context.Industry) is not { } threshold
            || settings.Classify((ratio - threshold) * 100m) is not { } severity)
        {
            return null;
        }

        return new RuleResult(
            severity,
            $"La pérdida de materiales equivale a {Fmt.Pct(ratio)} del costo de ventas en {context.Year} (umbral {Fmt.Pct(threshold)}).",
            "Mide las mermas por proceso y por lote, revisa almacenamiento y manejo de materiales y ajusta los parámetros de " +
            "producción con más desperdicio.",
            new JsonObject
            {
                ["year"] = context.Year,
                ["materialLoss"] = context.Financial.MaterialLoss,
                ["cogs"] = context.Financial.Cogs,
                ["ratio"] = Fmt.R(ratio),
                ["threshold"] = threshold,
            },
            null);
    }
}

/// <summary>LOW_LIQUIDITY: razón corriente menor que 1,0 → riesgo de liquidez (alerta financiera).</summary>
public sealed class LowLiquidityRule : IDiagnosticRule
{
    public string Code => "LOW_LIQUIDITY";

    public string Title => "Riesgo de liquidez";

    public ProblemCategory Category => ProblemCategory.FinancialStability;

    public ImpactCategory ImpactCategory => ImpactCategory.FinancialAlert;

    public RuleResult? Evaluate(DiagnosisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var settings = context.Settings.For(Code);
        if (context.Kpi(KpiCode.CurrentRatio) is not { } ratio || settings.ReferenceFor(context.Industry) is not { } minimum
            || settings.Classify(minimum - ratio) is not { } severity)
        {
            return null;
        }

        return new RuleResult(
            severity,
            $"La razón corriente de {context.Year} es {Fmt.Times(ratio)}: los activos corrientes no cubren los pasivos de corto plazo (mínimo {Fmt.Times(minimum)}).",
            "Prepara un flujo de caja de 13 semanas, acelera la cobranza, renegocia plazos con proveedores y evalúa " +
            "refinanciar deuda de corto a largo plazo.",
            new JsonObject { ["kpi"] = KpiCode.CurrentRatio.ToString(), ["year"] = context.Year, ["current"] = ratio, ["minimum"] = minimum },
            KpiCode.CurrentRatio);
    }
}

/// <summary>HIGH_LEVERAGE: pasivos / patrimonio sobre el umbral (3,0) → endeudamiento alto (alerta financiera).</summary>
public sealed class HighLeverageRule : IDiagnosticRule
{
    public string Code => "HIGH_LEVERAGE";

    public string Title => "Endeudamiento alto";

    public ProblemCategory Category => ProblemCategory.FinancialStability;

    public ImpactCategory ImpactCategory => ImpactCategory.FinancialAlert;

    public RuleResult? Evaluate(DiagnosisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var settings = context.Settings.For(Code);
        if (context.Kpi(KpiCode.DebtToEquity) is not { } ratio || settings.ReferenceFor(context.Industry) is not { } maximum
            || settings.Classify(ratio - maximum) is not { } severity)
        {
            return null;
        }

        return new RuleResult(
            severity,
            $"Los pasivos equivalen a {Fmt.Times(ratio)} veces el patrimonio en {context.Year} (máximo recomendado {Fmt.Times(maximum)}).",
            "Reduce deuda con la generación de caja, evita nuevo endeudamiento para gastos corrientes y evalúa aportes de " +
            "capital o capitalizar utilidades.",
            new JsonObject { ["kpi"] = KpiCode.DebtToEquity.ToString(), ["year"] = context.Year, ["current"] = ratio, ["maximum"] = maximum },
            KpiCode.DebtToEquity);
    }
}
