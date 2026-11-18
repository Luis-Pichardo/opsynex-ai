using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Opsynex.Api.Controllers;

/// <summary>Información general del API.</summary>
[ApiController]
[AllowAnonymous]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/system")]
public sealed class SystemController(IHostEnvironment environment) : ControllerBase
{
    /// <summary>Devuelve el nombre, la versión y el entorno del API.</summary>
    [HttpGet("info")]
    [ProducesResponseType<SystemInfoResponse>(StatusCodes.Status200OK)]
    public ActionResult<SystemInfoResponse> GetInfo() => Ok(new SystemInfoResponse(
        "Opsynex AI API",
        typeof(SystemController).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
        environment.EnvironmentName));
}

public sealed record SystemInfoResponse(string Name, string Version, string Environment);
