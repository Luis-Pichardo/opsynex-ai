using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Opsynex.Api.Auth;
using Opsynex.Application.Auth;
using Opsynex.Application.Users;

namespace Opsynex.Api.Controllers;

/// <summary>Autenticación. No hay auto-registro: solo un Admin crea usuarios.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/auth")]
[Produces("application/json")]
public sealed class AuthController(AuthService auth, UserService users) : ControllerBase
{
    /// <summary>Inicia sesión con correo y contraseña y devuelve un JWT.</summary>
    /// <remarks>Tras 5 intentos fallidos la cuenta se bloquea 15 minutos (423).</remarks>
    [HttpPost("login")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(Extensions.SecurityExtensions.LoginPolicy)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status423Locked)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken) =>
        Ok(await auth.LoginAsync(request, cancellationToken));

    /// <summary>Crea un usuario de cualquier rol (solo Admin). Equivale a <c>POST /users</c>.</summary>
    [HttpPost("register")]
    [Authorize(Policy = Policies.CanManage)]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> Register(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await users.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(UsersController.Get), "Users", new { id = user.Id, version = "1" }, user);
    }

    /// <summary>Datos del usuario autenticado.</summary>
    [HttpGet("me")]
    [Authorize(Policy = Policies.CanView)]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken) =>
        Ok(await auth.GetCurrentUserAsync(cancellationToken));
}
