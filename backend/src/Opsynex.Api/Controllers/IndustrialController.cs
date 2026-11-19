using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Opsynex.Api.Auth;
using Opsynex.Application.Industrial;

namespace Opsynex.Api.Controllers;

/// <summary>Módulo industrial: KPIs de producción y costos (OEE, scrap, paradas) y score industrial. Todos los roles.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/industrial")]
[Authorize(Policy = Policies.CanView)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
public sealed class IndustrialController(IndustrialService industrial) : ControllerBase
{
    /// <summary>
    /// KPIs industriales del período (año o mes) y del alcance (planta, línea o máquina): resumen,
    /// ranking por máquina, líneas, tendencia mensual, serie diaria, costos y pérdidas valoradas.
    /// Con <c>hasData = false</c> la empresa no tiene datos industriales (el menú Producción se oculta).
    /// </summary>
    /// <param name="year">Año (por defecto, el último con datos).</param>
    /// <param name="month">Mes 1–12 (por defecto, el año completo).</param>
    /// <param name="machineId">Máquina (p. ej. MAQ-01).</param>
    /// <param name="lineId">Línea (p. ej. LINEA-1).</param>
    /// <param name="cancellationToken">Cancelación.</param>
    [HttpGet("kpis")]
    [ProducesResponseType<IndustrialKpisDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IndustrialKpisDto>> Kpis(
        [FromQuery] int? year, [FromQuery] int? month, [FromQuery] string? machineId, [FromQuery] string? lineId, CancellationToken cancellationToken) =>
        Ok(await industrial.GetKpisAsync(new IndustrialFilter(year, month, machineId, lineId), cancellationToken));

    /// <summary>
    /// Score industrial (0–100): producción × 0,30 + costo × 0,25 + financiero × 0,25 + estándar × 0,20,
    /// con los mismos filtros que los KPIs. Junto al Score Opsynex del año.
    /// </summary>
    /// <param name="year">Año (por defecto, el último con datos).</param>
    /// <param name="month">Mes 1–12 (por defecto, el año completo).</param>
    /// <param name="machineId">Máquina.</param>
    /// <param name="lineId">Línea.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    [HttpGet("score")]
    [ProducesResponseType<IndustrialScoreDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IndustrialScoreDto>> Score(
        [FromQuery] int? year, [FromQuery] int? month, [FromQuery] string? machineId, [FromQuery] string? lineId, CancellationToken cancellationToken) =>
        Ok(await industrial.GetScoreAsync(new IndustrialFilter(year, month, machineId, lineId), cancellationToken));
}
