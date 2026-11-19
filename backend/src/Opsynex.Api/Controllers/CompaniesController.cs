using Opsynex.Api.Extensions;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Opsynex.Api.Auth;
using Opsynex.Application.Companies;

namespace Opsynex.Api.Controllers;

/// <summary>
/// Empresas. Ver: todos los roles (Analyst y Client solo ven la suya; otra empresa → 404).
/// Crear, modificar y eliminar: solo Admin.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/companies")]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class CompaniesController(CompanyService companies) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.CanView)]
    [ProducesResponseType<IReadOnlyList<CompanyDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CompanyDto>>> List(
        [FromQuery] string? search, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        Ok(Response.Page(await companies.ListAsync(search, cancellationToken), page, pageSize));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.CanView)]
    [ProducesResponseType<CompanyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await companies.GetAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Policy = Policies.CanManage)]
    [ProducesResponseType<CompanyDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CompanyDto>> Create(CompanyRequest request, CancellationToken cancellationToken)
    {
        var company = await companies.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = company.Id, version = "1" }, company);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.CanManage)]
    [ProducesResponseType<CompanyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyDto>> Update(Guid id, CompanyRequest request, CancellationToken cancellationToken) =>
        Ok(await companies.UpdateAsync(id, request, cancellationToken));

    /// <summary>Elimina una empresa sin usuarios ni datos (si los tiene: 409 <c>company.in_use</c>).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.CanManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await companies.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
