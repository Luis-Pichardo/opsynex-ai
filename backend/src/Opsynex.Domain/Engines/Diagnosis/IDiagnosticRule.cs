using System.Text.Json.Nodes;
using Opsynex.Domain.Engines.Kpis;
using Opsynex.Domain.Enums;

namespace Opsynex.Domain.Engines.Diagnosis;

/// <summary>Resultado de una regla que se disparó.</summary>
public sealed record RuleResult(
    Severity Severity,
    string Description,
    string Recommendation,
    JsonObject Evidence,
    KpiCode? RelatedKpi);

/// <summary>
/// Regla de diagnóstico. Para agregar una: implementar esta interfaz en
/// <c>Opsynex.Domain/Engines/Diagnosis/Rules</c> (se registra sola por DI), agregar sus umbrales por
/// defecto en <see cref="DiagnosisSettings.Defaults"/> y sus tests (dispara / no dispara).
/// </summary>
public interface IDiagnosticRule
{
    string Code { get; }

    string Title { get; }

    ProblemCategory Category { get; }

    ImpactCategory ImpactCategory { get; }

    /// <summary>Evalúa el año; <c>null</c> si no hay problema o faltan datos.</summary>
    RuleResult? Evaluate(DiagnosisContext context);
}
