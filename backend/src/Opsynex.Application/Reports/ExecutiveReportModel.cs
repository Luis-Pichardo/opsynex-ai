using Opsynex.Domain.Engines.Kpis;
using Opsynex.Domain.Enums;

namespace Opsynex.Application.Reports;

/// <summary>Componente del score en el informe.</summary>
public sealed record ReportScoreComponent(string Name, decimal? Value, decimal Weight, decimal BaseWeight, string? MissingReason);

/// <summary>Score del año con su interpretación.</summary>
public sealed record ReportScore(
    decimal Total,
    ScoreInterpretation Interpretation,
    string InterpretationLabel,
    decimal? ChangeFromPrevious,
    bool WeightsRedistributed,
    IReadOnlyList<ReportScoreComponent> Components);

public sealed record ReportImpactCategory(ImpactCategory Category, string Name, decimal EstimatedLoss, int Problems);

public sealed record ReportImpact(
    decimal TotalEstimatedLoss,
    decimal? Revenue,
    decimal? LossPercentOfRevenue,
    IReadOnlyList<ReportImpactCategory> ByCategory);

/// <summary>KPI del año con el valor del año anterior.</summary>
public sealed record ReportKpi(KpiCode Code, string Name, string Formula, KpiUnit Unit, bool HigherIsBetter, decimal? Value, decimal? Previous);

/// <summary>Punto de la serie de tendencia (ingresos y costo total por año).</summary>
public sealed record ReportTrendPoint(int Year, decimal? Revenue, decimal? TotalCost, decimal? EbitdaMargin);

public sealed record ReportProblem(
    int Priority,
    string Code,
    string Title,
    Severity Severity,
    ImpactCategory ImpactCategory,
    decimal? ImpactValue,
    string Description,
    string Recommendation,
    string? AiRecommendation);

public sealed record ReportWarning(string Message, string? Location);

/// <summary>Todo lo que contiene el informe ejecutivo en PDF de una empresa y un año.</summary>
public sealed record ExecutiveReportModel(
    string CompanyName,
    Industry Industry,
    string Currency,
    int Year,
    int? PreviousYear,
    DateTime GeneratedAt,
    ReportScore? Score,
    ReportImpact Impact,
    IReadOnlyList<ReportKpi> Kpis,
    IReadOnlyList<ReportTrendPoint> Trend,
    IReadOnlyList<ReportProblem> Problems,
    IReadOnlyList<ReportWarning> Warnings,
    string? ExecutiveSummary = null);

/// <summary>Genera el PDF del informe ejecutivo (implementación en Infrastructure con QuestPDF).</summary>
public interface IExecutiveReportRenderer
{
    byte[] Render(ExecutiveReportModel model);
}

/// <summary>Archivo generado.</summary>
public sealed record ReportFile(string FileName, byte[] Content);
