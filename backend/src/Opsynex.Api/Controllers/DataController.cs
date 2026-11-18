using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Opsynex.Api.Auth;
using Opsynex.Application.Uploads;
using Opsynex.Domain.Enums;

namespace Opsynex.Api.Controllers;

/// <summary>Datos cargados de la empresa (en bruto, consolidados, operacionales o mensuales).</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/data")]
[Authorize(Policy = Policies.CanView)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class DataController(DataService data) : ControllerBase
{
    /// <summary>Consulta los datos de la empresa.</summary>
    /// <param name="year">Año (opcional).</param>
    /// <param name="type">raw (por defecto), financial, operational o monthly.</param>
    /// <param name="sheetType">Solo para raw: filtra por tipo de hoja.</param>
    /// <param name="uploadId">Solo para raw: una carga concreta (por defecto, la más reciente que tenga el año).</param>
    /// <param name="cancellationToken">Cancelación.</param>
    [HttpGet]
    [ProducesResponseType<DataResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DataResponse>> Get(
        [FromQuery] int? year,
        [FromQuery] DataType type,
        [FromQuery] SheetType? sheetType,
        [FromQuery] Guid? uploadId,
        CancellationToken cancellationToken) =>
        Ok(await data.GetAsync(new DataQuery(year, type, sheetType, uploadId), cancellationToken));
}
