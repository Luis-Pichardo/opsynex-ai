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
public sealed class AnalysisController(AnalysisPipeline pipeline, AnalysisSummaryService summary, CompanyScope scope) : ControllerBase
{
    /// <summary>Resumen del último año analizado: años, KPIs calculados, problemas por severidad, score e impacto.</summary>
    [HttpGet("summary")]
    [Authorize(Policy = Policies.CanView)]
    [ProducesResponseType<AnalysisSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AnalysisSummaryDto>> Summary(CancellationToken cancellationToken) =>
        Ok(await summary.GetAsync(cancellationToken));

    /// <summary>Recalcula los KPIs de todos los años de la empresa (el Admin la elige con X-Company-Id o companyId).</summary>
    [HttpPost("run")]
    [Authorize(Policy = Policies.CanAnalyze)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<AnalysisRunResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AnalysisRunResult>> Run(CancellationToken cancellationToken) =>
        Ok(await pipeline.RunAsync(scope.Require("analysis.company_required"), cancellationToken));
}
