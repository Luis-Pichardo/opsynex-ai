using Opsynex.Application.Abstractions.Persistence;
using Opsynex.Application.Common.Exceptions;
using Opsynex.Domain.Engines.Kpis;
using Opsynex.Domain.Engines.Scoring;
using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;

namespace Opsynex.Application.Analysis;

/// <summary>Problemas del año por severidad.</summary>
public sealed record ProblemCountsDto(int Total, int High, int Medium, int Low);

/// <summary>
/// Resumen del análisis automático del último año: lo que muestra el asistente de carga al
/// terminar (años, KPIs calculados, problemas, score e impacto).
/// </summary>
public sealed record AnalysisSummaryDto(
    Guid CompanyId,
    string Currency,
    IReadOnlyList<int> Years,
    int? LatestYear,
    int KpisCalculated,
    int KpisTotal,
    ProblemCountsDto Problems,
    decimal? Score,
    ScoreInterpretation? Interpretation,
    string? InterpretationLabel,
    decimal? EstimatedLoss,
    DateTime? CalculatedAt);

public sealed class AnalysisSummaryService(
    CompanyScope scope,
    ICompanyRepository companies,
    ICompanyYearRepository<KpiResult> kpiResults,
    ICompanyYearRepository<Score> scores,
    IRepository<Problem> problems)
{
    public async Task<AnalysisSummaryDto> GetAsync(CancellationToken cancellationToken)
    {
        var companyId = scope.Require("analysis.company_required");
        var company = await companies.GetByIdAsync(companyId, cancellationToken) ?? throw new NotFoundException("la empresa", companyId);
        var kpis = await kpiResults.ListByCompanyAsync(companyId, cancellationToken);
        var latest = kpis.Count == 0 ? null : kpis.MaxBy(k => k.Year);
        if (latest is null)
        {
            return new AnalysisSummaryDto(companyId, company.Currency, [], null, 0, KpiCatalog.All.Count,
                new ProblemCountsDto(0, 0, 0, 0), null, null, null, null, null);
        }

        var yearProblems = await problems.ListAsync(p => p.CompanyId == companyId && p.Year == latest.Year, cancellationToken);
        var score = await scores.GetByCompanyAndYearAsync(companyId, latest.Year, cancellationToken);
        return new AnalysisSummaryDto(
            companyId,
            company.Currency,
            [.. kpis.Select(k => k.Year).Order()],
            latest.Year,
            latest.Values().Count(v => v.Value is not null),
            KpiCatalog.All.Count,
            new ProblemCountsDto(
                yearProblems.Count,
                yearProblems.Count(p => p.Severity == Severity.High),
                yearProblems.Count(p => p.Severity == Severity.Medium),
                yearProblems.Count(p => p.Severity == Severity.Low)),
            score?.TotalScore,
            score?.Interpretation,
            score is null ? null : ScoreEngine.Label(score.Interpretation),
            yearProblems.Sum(p => p.ImpactValue ?? 0m),
            latest.CalculatedAt);
    }
}
