using System.Text.Json.Nodes;
using Opsynex.Application.Abstractions.Persistence;
using Opsynex.Application.Common.Exceptions;
using Opsynex.Domain.Engines.Diagnosis;
using Opsynex.Domain.Engines.Kpis;
using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;

namespace Opsynex.Application.Analysis;

public sealed record ProblemQuery(int? Year, Severity? Severity, ProblemCategory? Category, ProblemStatus? Status, int Page = 1, int PageSize = 20);

public sealed record ProblemListItemDto(
    Guid Id,
    int Year,
    string Code,
    string Title,
    string Description,
    ProblemCategory Category,
    Severity Severity,
    ImpactCategory ImpactCategory,
    decimal? ImpactValue,
    ProblemStatus Status,
    string Recommendation,
    int Priority);

/// <summary>Valor de un KPI en un año (historial del KPI relacionado con el problema).</summary>
public sealed record KpiHistoryPoint(int Year, decimal? Value, decimal? Normalized);

public sealed record ProblemDetailDto(
    Guid Id,
    int Year,
    string Code,
    string Title,
    string Description,
    ProblemCategory Category,
    Severity Severity,
    ImpactCategory ImpactCategory,
    decimal? ImpactValue,
    ProblemStatus Status,
    string Recommendation,
    Insights.AiRecommendation? AiRecommendation,
    JsonObject Evidence,
    KpiCode? RelatedKpi,
    IReadOnlyList<KpiHistoryPoint> KpiHistory,
    DateTime UpdatedAt);

public sealed record ProblemPage(
    IReadOnlyList<ProblemListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<Highlight> Highlights);

public sealed record UpdateProblemStatusRequest(ProblemStatus Status);

/// <summary>Consulta y seguimiento de los problemas del diagnóstico.</summary>
public sealed class ProblemService(
    CompanyScope scope,
    IRepository<Problem> problems,
    ICompanyYearRepository<KpiResult> kpiResults,
    IUnitOfWork unitOfWork)
{
    public const int MaxPageSize = 100;

    /// <summary>Problemas de la empresa ordenados por prioridad (severidad y luego impacto económico).</summary>
    public async Task<ProblemPage> ListAsync(ProblemQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var companyId = scope.Require("problems.company_required");
        if (query.Page < 1 || query.PageSize is < 1 or > MaxPageSize)
        {
            throw new BusinessValidationException("problems.invalid_page", "pageSize", $"page debe ser ≥ 1 y pageSize estar entre 1 y {MaxPageSize}.");
        }

        var all = await problems.ListAsync(
            p => p.CompanyId == companyId
                && (query.Year == null || p.Year == query.Year)
                && (query.Severity == null || p.Severity == query.Severity)
                && (query.Category == null || p.Category == query.Category)
                && (query.Status == null || p.Status == query.Status),
            cancellationToken);

        var ordered = DiagnosisEngine.Prioritize(all, p => p.Severity, p => p.ImpactValue)
            .ThenByDescending(p => p.Year)
            .ThenBy(p => p.Code, StringComparer.Ordinal)
            .ToList();
        var items = ordered
            .Select((p, index) => ToListItem(p, index + 1))
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        var kpis = await kpiResults.ListByCompanyAsync(companyId, cancellationToken);
        var highlights = kpis.Where(k => query.Year is null ? k.Year == kpis.Max(x => x.Year) : k.Year == query.Year)
            .SelectMany(k => k.Highlights)
            .ToList();

        return new ProblemPage(items, query.Page, query.PageSize, ordered.Count, highlights);
    }

    /// <summary>Detalle con la evidencia y el historial del KPI relacionado. Otra empresa → 404.</summary>
    public async Task<ProblemDetailDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var problem = await problems.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("el problema", id);
        KpiCode? relatedKpi = problem.Evidence["kpi"]?.GetValue<string>() is { } name && Enum.TryParse<KpiCode>(name, out var code) ? code : null;

        var history = new List<KpiHistoryPoint>();
        if (relatedKpi is { } kpi)
        {
            var series = await kpiResults.ListByCompanyAsync(problem.CompanyId, cancellationToken);
            history.AddRange(series.Select(k => new KpiHistoryPoint(k.Year, k.Get(kpi), k.Normalized.TryGetValue(kpi.ToString(), out var n) ? n : null)));
        }

        return new ProblemDetailDto(problem.Id, problem.Year, problem.Code, problem.Title, problem.Description, problem.Category,
            problem.Severity, problem.ImpactCategory, problem.ImpactValue, problem.Status, problem.Recommendation, Insights.AiEnrichmentService.ParseRecommendation(problem.AiRecommendation),
            problem.Evidence, relatedKpi, history, problem.UpdatedAt);
    }

    /// <summary>Cambia el estado de seguimiento (Admin y Analyst). Se conserva al volver a analizar.</summary>
    public async Task<ProblemDetailDto> UpdateStatusAsync(Guid id, UpdateProblemStatusRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(request.Status))
        {
            throw new BusinessValidationException("problems.invalid_status", "status", "El estado no es válido (Open, Acknowledged o Resolved).");
        }

        var problem = await problems.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("el problema", id);
        problem.Status = request.Status;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    private static ProblemListItemDto ToListItem(Problem p, int priority) => new(
        p.Id, p.Year, p.Code, p.Title, p.Description, p.Category, p.Severity, p.ImpactCategory, p.ImpactValue, p.Status, p.Recommendation, priority);
}
