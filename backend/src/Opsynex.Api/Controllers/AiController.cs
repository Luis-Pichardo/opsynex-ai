using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Opsynex.Api.Auth;
using Opsynex.Application.Insights;

namespace Opsynex.Api.Controllers;

/// <summary>Análisis con IA: resumen ejecutivo (todos) y regeneración (Admin y Analyst).</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/ai")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class AiController(AiInsightsService ai) : ControllerBase
{
    /// <summary>Resumen ejecutivo con IA del año (por defecto el último) y el estado de la IA.</summary>
    /// <param name="year">Año (opcional).</param>
    /// <param name="cancellationToken">Cancelación.</param>
    [HttpGet("summary")]
    [Authorize(Policy = Policies.CanView)]
    [ProducesResponseType<AiSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AiSummaryDto>> Summary([FromQuery] int? year, CancellationToken cancellationToken) =>
        Ok(await ai.GetSummaryAsync(year, cancellationToken));

    /// <summary>Vuelve a generar el resumen y las recomendaciones con IA (en segundo plano: 202).</summary>
    /// <param name="year">Año (opcional).</param>
    /// <param name="cancellationToken">Cancelación.</param>
    [HttpPost("regenerate")]
    [Authorize(Policy = Policies.CanAnalyze)]
    [ProducesResponseType<AiSummaryDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AiSummaryDto>> Regenerate([FromQuery] int? year, CancellationToken cancellationToken) =>
        Accepted(await ai.RegenerateAsync(year, cancellationToken));
}
