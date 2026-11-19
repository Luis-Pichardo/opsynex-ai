using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Opsynex.Api.Auth;
using Opsynex.Application.Homologation;

namespace Opsynex.Api.Controllers;

/// <summary>
/// Homologación contable. El análisis es automático al subir un archivo; aquí el analista revisa
/// (opcional) las cuentas, corrige o confirma su categoría y vuelve a analizar.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/mappings")]
[Authorize(Policy = Policies.CanAnalyze)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class MappingsController(HomologationService homologation) : ControllerBase
{
    /// <summary>Mapeos de la empresa (el Admin elige la empresa con X-Company-Id o companyId).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MappingDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<MappingDto>>> List(CancellationToken cancellationToken) =>
        Ok(await homologation.ListAsync(cancellationToken));

    /// <summary>Catálogo de categorías estándar con nombre y descripción en español.</summary>
    [HttpGet("categories")]
    [ProducesResponseType<IReadOnlyList<CategoryDto>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<CategoryDto>> Categories() => Ok(CategoryDto.All);

    /// <summary>Cuentas de una carga sin confirmar con su sugerencia; primero las dudosas.</summary>
    [HttpGet("pending")]
    [ProducesResponseType<IReadOnlyList<PendingAccountDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<PendingAccountDto>>> Pending([FromQuery] Guid uploadId, CancellationToken cancellationToken) =>
        Ok(await homologation.PendingAsync(uploadId, cancellationToken));

    /// <summary>Guarda o confirma mapeos (masivo). Quedan confirmados y se reutilizan en las cargas siguientes.</summary>
    [HttpPut]
    [ProducesResponseType<IReadOnlyList<MappingDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<MappingDto>>> Save(SaveMappingsRequest request, CancellationToken cancellationToken) =>
        Ok(await homologation.SaveAsync(request, cancellationToken));

    /// <summary>Vuelve a analizar una carga con los mapeos actuales y actualiza FinancialData (todos sus años).</summary>
    [HttpPost("consolidate")]
    [ProducesResponseType<ProcessingResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProcessingResult>> Consolidate([FromQuery] Guid uploadId, CancellationToken cancellationToken) =>
        Ok(await homologation.ProcessUploadAsync(uploadId, cancellationToken));
}
