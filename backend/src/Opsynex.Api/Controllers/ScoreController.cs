using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Opsynex.Api.Auth;
using Opsynex.Application.Analysis;

namespace Opsynex.Api.Controllers;

/// <summary>Score Opsynex de la empresa (todos los roles).</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/score")]
[Authorize(Policy = Policies.CanView)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class ScoreController(ScoreService scores) : ControllerBase
{
    /// <summary>Score (0–100) con componentes, pesos usados e interpretación. Sin <c>year</c> devuelve la serie histórica.</summary>
    /// <param name="year">Año (opcional).</param>
    /// <param name="cancellationToken">Cancelación.</param>
    [HttpGet]
    [ProducesResponseType<ScoreResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ScoreResponse>> Get([FromQuery] int? year, CancellationToken cancellationToken) =>
        Ok(await scores.GetAsync(year, cancellationToken));
}
