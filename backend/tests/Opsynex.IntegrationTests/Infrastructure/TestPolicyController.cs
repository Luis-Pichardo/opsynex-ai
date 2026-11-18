using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Opsynex.Api.Auth;

namespace Opsynex.IntegrationTests.Infrastructure;

/// <summary>
/// Endpoints solo de pruebas, uno por política, para verificar la matriz de permisos aunque
/// los endpoints reales de carga y análisis lleguen en etapas posteriores.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/__test/policies")]
public sealed class TestPolicyController : ControllerBase
{
    [HttpGet("manage")]
    [Authorize(Policy = Policies.CanManage)]
    public IActionResult Manage() => Ok();

    [HttpGet("analyze")]
    [Authorize(Policy = Policies.CanAnalyze)]
    public IActionResult Analyze() => Ok();

    [HttpGet("view")]
    [Authorize(Policy = Policies.CanView)]
    public IActionResult View() => Ok();

    [HttpGet("default")]
    public IActionResult Default() => Ok();
}
