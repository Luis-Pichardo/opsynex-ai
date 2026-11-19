using Opsynex.Domain.Engines.Kpis;
using Opsynex.Domain.Enums;

namespace Opsynex.Domain.Engines.Diagnosis;

/// <summary>Problema detectado por una regla.</summary>
public sealed record DetectedProblem(IDiagnosticRule Rule, RuleResult Result)
{
    public string Code => Rule.Code;

    public Severity Severity => Result.Severity;
}

/// <summary>Hallazgo positivo (para que el resumen no muestre solo problemas).</summary>
public sealed record Highlight(string Code, string Title, string Detail);

/// <summary>Resultado del diagnóstico de un año.</summary>
public sealed record DiagnosisResult(int Year, IReadOnlyList<DetectedProblem> Problems, IReadOnlyList<Highlight> Highlights);

/// <summary>
/// Motor de diagnóstico (sección 11): evalúa las reglas activas sobre un año, prioriza los
/// problemas (severidad y luego impacto económico) y registra hallazgos positivos.
/// </summary>
public sealed class DiagnosisEngine(IEnumerable<IDiagnosticRule> rules)
{
    private readonly IReadOnlyList<IDiagnosticRule> _rules = rules.ToList();

    public IReadOnlyList<IDiagnosticRule> Rules => _rules;

    public DiagnosisResult Evaluate(DiagnosisContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var problems = _rules
            .Where(r => context.Settings.For(r.Code).Enabled)
            .Select(r => (Rule: r, Result: r.Evaluate(context)))
            .Where(x => x.Result is not null)
            .Select(x => new DetectedProblem(x.Rule, x.Result!))
            .OrderByDescending(p => p.Severity)
            .ThenBy(p => p.Code, StringComparer.Ordinal)
            .ToList();
        return new DiagnosisResult(context.Year, problems, Highlights(context));
    }

    /// <summary>Orden de prioridad: severidad (High primero) y luego impacto económico (mayor primero, sin impacto al final).</summary>
    public static IOrderedEnumerable<T> Prioritize<T>(IEnumerable<T> items, Func<T, Severity> severity, Func<T, decimal?> impact) =>
        items.OrderByDescending(severity).ThenByDescending(i => impact(i) ?? decimal.MinValue);

    private static List<Highlight> Highlights(DiagnosisContext context)
    {
        var result = new List<Highlight>();
        if (!context.HasPrevious)
        {
            return result;
        }

        void Improved(KpiCode code, string title, decimal minChange, bool higherIsBetter, Func<decimal, decimal, string> detail)
        {
            if (context.PreviousKpi(code) is { } p && context.Kpi(code) is { } c && (higherIsBetter ? c - p : p - c) >= minChange)
            {
                result.Add(new Highlight(code.ToString(), title, detail(p, c)));
            }
        }

        Improved(KpiCode.EbitdaMargin, "Mejora del margen EBITDA", 0.005m, true,
            (p, c) => $"El margen EBITDA subió de {Fmt.Pct(p)} a {Fmt.Pct(c)} ({Fmt.Pp((c - p) * 100m)}).");
        Improved(KpiCode.GrossMargin, "Mejora del margen bruto", 0.005m, true,
            (p, c) => $"El margen bruto subió de {Fmt.Pct(p)} a {Fmt.Pct(c)}.");
        Improved(KpiCode.DebtToEquity, "Menor endeudamiento", 0.05m, false,
            (p, c) => $"Los pasivos bajaron de {Fmt.Times(p)} a {Fmt.Times(c)} veces el patrimonio.");
        Improved(KpiCode.CurrentRatio, "Mejor liquidez", 0.1m, true,
            (p, c) => $"La razón corriente subió de {Fmt.Times(p)} a {Fmt.Times(c)}.");
        Improved(KpiCode.CostEfficiency, "Costos más eficientes", 0.005m, false,
            (p, c) => $"El costo total por cada peso de ingreso bajó de {Fmt.Pct(p)} a {Fmt.Pct(c)}.");

        if (DiagnosisContext.Growth(context.PreviousFinancial!.Revenue, context.Financial.Revenue) is { } growth && growth >= 5m)
        {
            result.Add(new Highlight("RevenueGrowth", "Crecimiento de ingresos", $"Los ingresos crecieron {Fmt.Percent(growth)}."));
        }

        return result;
    }
}
