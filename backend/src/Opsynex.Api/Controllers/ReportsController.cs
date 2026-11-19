using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Opsynex.Api.Auth;
using Opsynex.Application.Reports;

namespace Opsynex.Api.Controllers;

/// <summary>Informes descargables (todos los roles, incluido Client).</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/reports")]
[Authorize(Policy = Policies.CanView)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class ReportsController(ReportService reports) : ControllerBase
{
    /// <summary>Informe ejecutivo en PDF: score, impacto, KPIs, tendencia, problemas, calidad de datos y nota metodológica.</summary>
    /// <param name="year">Año (opcional; por defecto el último analizado).</param>
    /// <param name="cancellationToken">Cancelación.</param>
    [HttpGet("executive")]
    [Produces("application/pdf")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK, "application/pdf")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Executive([FromQuery] int? year, CancellationToken cancellationToken)
    {
        var report = await reports.ExecutiveAsync(year, cancellationToken);
        return File(report.Content, "application/pdf", report.FileName);
    }
}
