using Opsynex.Api.Extensions;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Opsynex.Api.Auth;
using Opsynex.Application.Auth;
using Opsynex.Application.Users;
using Opsynex.Domain.Enums;

namespace Opsynex.Api.Controllers;

/// <summary>Gestión de usuarios (solo Admin).</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/users")]
[Authorize(Policy = Policies.CanManage)]
[Produces("application/json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class UsersController(UserService users) : ControllerBase
{
    /// <summary>Lista usuarios, con filtros opcionales por empresa, rol y texto.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<UserDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> List(
        [FromQuery] Guid? companyId,
        [FromQuery] UserRole? role,
        [FromQuery] string? search,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken) =>
        Ok(Response.Page(await users.ListAsync(new UserQuery(companyId, role, search), cancellationToken), page, pageSize));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await users.GetAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await users.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = user.Id, version = "1" }, user);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> Update(Guid id, UpdateUserRequest request, CancellationToken cancellationToken) =>
        Ok(await users.UpdateAsync(id, request, cancellationToken));

    /// <summary>Asigna una contraseña nueva y quita el bloqueo temporal.</summary>
    [HttpPut("{id:guid}/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPassword(Guid id, SetPasswordRequest request, CancellationToken cancellationToken)
    {
        await users.SetPasswordAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Desactiva el usuario (no se borra físicamente).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await users.DeactivateAsync(id, cancellationToken);
        return NoContent();
    }
}
