using Microsoft.Extensions.Options;
using Opsynex.Application.Abstractions.Persistence;
using Opsynex.Application.Analysis;
using Opsynex.Application.Common.Exceptions;
using Opsynex.Domain.Entities;

namespace Opsynex.Application.Insights;

/// <summary>
/// Estado y contenido del resumen ejecutivo con IA de un año.
/// </summary>
/// <param name="CompanyId">Empresa.</param>
/// <param name="Year">Año (el último analizado si no se indica).</param>
/// <param name="Status">
/// <c>ready</c> (hay resumen), <c>pending</c> (generándose), <c>none</c> (todavía no hay),
/// <c>disabled</c> (Ai:Enabled = false), <c>not_configured</c> (sin clave de Claude) o
/// <c>unavailable</c> (el servicio de IA o Claude fallaron).
/// </param>
/// <param name="Paragraphs">Párrafos del resumen (vacío si no hay).</param>
/// <param name="Priorities">Prioridades (vacío si no hay).</param>
/// <param name="Model">Modelo que lo generó.</param>
/// <param name="GeneratedAt">Fecha de generación (UTC).</param>
/// <param name="Message">Explicación para el usuario cuando no hay resumen o falló el último intento.</param>
public sealed record AiSummaryDto(
    Guid CompanyId,
    int? Year,
    string Status,
    IReadOnlyList<string> Paragraphs,
    IReadOnlyList<string> Priorities,
    string? Model,
    DateTime? GeneratedAt,
    string? Message);

/// <summary>Consulta del resumen con IA y regeneración bajo demanda.</summary>
public sealed class AiInsightsService(
    CompanyScope scope,
    ICompanyYearRepository<KpiResult> kpiResults,
    ICompanyYearRepository<ExecutiveSummary> summaries,
    IAiEnrichmentScheduler scheduler,
    AiJobTracker tracker,
    IOptions<AiOptions> options)
{
    public async Task<AiSummaryDto> GetSummaryAsync(int? year, CancellationToken cancellationToken)
    {
        var companyId = scope.Require("ai.company_required");
        var target = await ResolveYearAsync(companyId, year, cancellationToken);
        var summary = target is null ? null : await summaries.GetByCompanyAndYearAsync(companyId, target.Value, cancellationToken);
        var job = tracker.Get(companyId);

        var (status, message) = (options.Value.Enabled, summary, job?.State) switch
        {
            (false, _, _) => ("disabled", "La IA está desactivada en esta instalación. Se muestran las recomendaciones base."),
            (_, _, AiJobState.Pending or AiJobState.Running) => ("pending", "Generando el análisis con IA…"),
            (_, not null, AiJobState.Failed) => ("ready", $"No se pudo actualizar: {job!.Message}"),
            (_, not null, _) => ("ready", (string?)null),
            (_, null, AiJobState.Failed) when job!.ErrorCode == "not_configured" =>
                ("not_configured", "La IA generativa no está configurada. Las recomendaciones base siguen disponibles."),
            (_, null, AiJobState.Failed) => ("unavailable", job!.Message ?? "El servicio de IA no está disponible."),
            _ => ("none", target is null ? "Todavía no hay análisis para resumir." : "Todavía no se generó el resumen con IA."),
        };

        return new AiSummaryDto(
            companyId,
            target,
            status,
            summary?.Paragraphs.ToList() ?? [],
            summary?.Priorities.ToList() ?? [],
            summary?.Model,
            summary?.GeneratedAt,
            message);
    }

    /// <summary>Vuelve a generar el análisis con IA del año (aunque los datos no hayan cambiado).</summary>
    public async Task<AiSummaryDto> RegenerateAsync(int? year, CancellationToken cancellationToken)
    {
        var companyId = scope.Require("ai.company_required");
        if (!options.Value.Enabled)
        {
            throw new BusinessValidationException("ai.disabled", "Ai", "La IA está desactivada en esta instalación (Ai:Enabled = false).");
        }

        var target = await ResolveYearAsync(companyId, year, cancellationToken)
            ?? throw new BusinessValidationException("ai.no_data", "year", "La empresa todavía no tiene análisis. Sube sus estados financieros primero.");
        await scheduler.ScheduleAsync(companyId, target, force: true, cancellationToken);
        return await GetSummaryAsync(target, cancellationToken);
    }

    private async Task<int?> ResolveYearAsync(Guid companyId, int? year, CancellationToken cancellationToken)
    {
        if (year is < 1900 or > 2200)
        {
            throw new BusinessValidationException("ai.invalid_year", "year", "El año debe estar entre 1900 y 2200.");
        }

        var kpis = await kpiResults.ListByCompanyAsync(companyId, cancellationToken);
        return year is null ? (kpis.Count == 0 ? null : kpis.Max(k => k.Year)) : kpis.Any(k => k.Year == year) ? year : null;
    }
}
