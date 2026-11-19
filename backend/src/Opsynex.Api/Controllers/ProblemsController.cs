using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Opsynex.Api.Auth;
using Opsynex.Application.Analysis;
using Opsynex.Domain.Enums;

namespace Opsynex.Api.Controllers;

/// <summary>Problemas detectados por el motor de diagnóstico.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/problems")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class ProblemsController(ProblemService problems) : ControllerBase
{
    /// <summary>Problemas de la empresa por prioridad (severidad, luego impacto), con filtros y paginado; incluye los hallazgos positivos.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.CanView)]
    [ProducesResponseType<ProblemPage>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProblemPage>> List(
        [FromQuery] int? year,
        [FromQuery] Severity? severity,
        [FromQuery] ProblemCategory? category,
        [FromQuery] ProblemStatus? status,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        Ok(await problems.ListAsync(new ProblemQuery(year, severity, category, status, page, pageSize), cancellationToken));

    /// <summary>Detalle con la evidencia y el historial del KPI relacionado.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.CanView)]
    [ProducesResponseType<ProblemDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProblemDetailDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await problems.GetAsync(id, cancellationToken));

    /// <summary>Cambia el estado (Open, Acknowledged, Resolved). Admin y Analyst.</summary>
    [HttpPatch("{id:guid}")]
    [Authorize(Policy = Policies.CanAnalyze)]
    [ProducesResponseType<ProblemDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProblemDetailDto>> UpdateStatus(Guid id, UpdateProblemStatusRequest request, CancellationToken cancellationToken) =>
        Ok(await problems.UpdateStatusAsync(id, request, cancellationToken));
}
