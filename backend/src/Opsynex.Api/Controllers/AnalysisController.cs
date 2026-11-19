using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Opsynex.Api.Auth;
using Opsynex.Application.Analysis;

namespace Opsynex.Api.Controllers;

/// <summary>Análisis: resumen (todos) y ejecución bajo demanda (Admin y Analyst). También se ejecuta solo al consolidar una carga.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/analysis")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class AnalysisController(
    AnalysisPipeline pipeline,
    Application.Industrial.IndustrialAnalysisService industrial,
    AnalysisSummaryService summary,
    CompanyScope scope) : ControllerBase
{
    /// <summary>Resumen del último año analizado: años, KPIs calculados, problemas por severidad, score e impacto.</summary>
    [HttpGet("summary")]
    [Authorize(Policy = Policies.CanView)]
    [ProducesResponseType<AnalysisSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AnalysisSummaryDto>> Summary(CancellationToken cancellationToken) =>
        Ok(await summary.GetAsync(cancellationToken));

    /// <summary>
    /// Recalcula el análisis de la empresa (el Admin la elige con X-Company-Id o companyId): primero el
    /// industrial (si hay producción) y después el financiero de todos los años.
    /// </summary>
    [HttpPost("run")]
    [Authorize(Policy = Policies.CanAnalyze)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AnalysisRunResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AnalysisRunResult>> Run(CancellationToken cancellationToken)
    {
        var companyId = scope.Require("analysis.company_required");
        var industrialResult = await industrial.RunAsync(companyId, scheduleAi: false, cancellationToken);
        var result = await pipeline.RunAsync(companyId, cancellationToken);

        // El pipeline financiero programa la IA si hay años financieros; si solo hay datos industriales, se programa aquí.
        if (result.LatestYear is null && industrialResult.HasData)
        {
            await industrial.ScheduleAiAsync(companyId, cancellationToken);
        }

        return Ok(result with { IndustrialProblems = industrialResult.Problems });
    }
}
