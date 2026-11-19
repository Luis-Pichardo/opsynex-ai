using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Opsynex.Api.Auth;
using Opsynex.Application.Analysis;

namespace Opsynex.Api.Controllers;

/// <summary>Impacto financiero estimado de los problemas (todos los roles).</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/impact")]
[Authorize(Policy = Policies.CanView)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class ImpactController(ImpactService impact) : ControllerBase
{
    /// <summary>Pérdida total estimada del año, desglose por categoría y los 5 problemas de mayor impacto.</summary>
    /// <param name="year">Año (opcional; por defecto el último con datos).</param>
    /// <param name="cancellationToken">Cancelación.</param>
    [HttpGet("summary")]
    [ProducesResponseType<ImpactSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImpactSummaryDto>> Summary([FromQuery] int? year, CancellationToken cancellationToken) =>
        Ok(await impact.GetSummaryAsync(year, cancellationToken));
}
