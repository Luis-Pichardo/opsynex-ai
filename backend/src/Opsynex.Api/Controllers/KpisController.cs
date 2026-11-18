using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Opsynex.Api.Auth;
using Opsynex.Application.Analysis;

namespace Opsynex.Api.Controllers;

/// <summary>KPIs de la empresa (todos los roles).</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/kpis")]
[Authorize(Policy = Policies.CanView)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class KpisController(KpiService kpis) : ControllerBase
{
    /// <summary>KPIs con tendencias y normalización. Sin <c>year</c> devuelve la serie histórica; incluye la serie mensual si existe.</summary>
    /// <param name="year">Año (opcional).</param>
    /// <param name="cancellationToken">Cancelación.</param>
    [HttpGet]
    [ProducesResponseType<KpisResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KpisResponse>> Get([FromQuery] int? year, CancellationToken cancellationToken) =>
        Ok(await kpis.GetAsync(year, cancellationToken));
}
