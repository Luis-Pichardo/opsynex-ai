using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Opsynex.Api.Auth;
using Opsynex.Application.Common.Exceptions;
using Opsynex.Application.Uploads;
using Opsynex.Domain.Enums;

namespace Opsynex.Api.Controllers;

/// <summary>Carga de archivos: libro contable completo (.xls/.xlsx) o plantilla simple (.xlsx/.csv).</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/uploads")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class UploadsController(UploadService uploads, IUploadTemplateGenerator templates, TimeProvider timeProvider) : ControllerBase
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Sube y analiza un archivo (Admin y Analyst).</summary>
    /// <remarks>
    /// El tipo de cada hoja se detecta por su contenido. Responde 201 con el reporte si el archivo es
    /// válido (las inconsistencias son advertencias) o 422 con el mismo reporte si tiene errores
    /// bloqueantes (ilegible, vacío o sin balance ni estado de resultados). El Admin indica la empresa
    /// con <c>companyId</c>; el Analyst usa la suya.
    /// </remarks>
    /// <param name="file">Archivo .xls, .xlsx o .csv.</param>
    /// <param name="companyId">Empresa (solo Admin).</param>
    /// <param name="fileType">Tipo de archivo; opcional (se detecta), necesario para un CSV sin título.</param>
    /// <param name="cancellationToken">Cancelación.</param>
    [HttpPost]
    [Authorize(Policy = Policies.CanAnalyze)]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(Extensions.SecurityExtensions.UploadPolicy)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<UploadReportDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<UploadReportDto>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<UploadReportDto>> Upload(
        IFormFile? file,
        [FromForm] Guid? companyId,
        [FromForm] UploadFileType? fileType,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new BusinessValidationException("upload.file_required", "file", "Adjunta un archivo .xls, .xlsx o .csv en el campo «file».");
        }

        await using var stream = file.OpenReadStream();
        var report = await uploads.UploadAsync(
            new UploadFileRequest(stream, file.FileName, file.Length, companyId, fileType), cancellationToken);

        return report.Status == UploadStatus.Failed
            ? UnprocessableEntity(report)
            : CreatedAtAction(nameof(Get), new { id = report.UploadId, version = "1" }, report);
    }

    /// <summary>Cargas de la empresa, de la más reciente a la más antigua.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.CanView)]
    [ProducesResponseType<IReadOnlyList<UploadListItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UploadListItemDto>>> List(CancellationToken cancellationToken) =>
        Ok(await uploads.ListAsync(cancellationToken));

    /// <summary>Reporte completo de una carga (hojas, advertencias, errores, resumen y vista previa).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.CanView)]
    [ProducesResponseType<UploadReportDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UploadReportDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await uploads.GetAsync(id, cancellationToken));

    /// <summary>Plantilla simple .xlsx (alternativa para clientes sin estados financieros formales).</summary>
    /// <param name="fileType">IncomeStatement, BalanceSheet, CashFlow, CostStructure u Operational.</param>
    [HttpGet("templates/{fileType}")]
    [Authorize(Policy = Policies.CanAnalyze)]
    [Produces(XlsxContentType, "application/problem+json")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK, XlsxContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public IActionResult Template(UploadFileType fileType)
    {
        var fileName = UploadTemplates.Find(fileType)?.FileName
            ?? IndustrialTemplates.Find(fileType)?.FileName
            ?? throw new NotFoundException("la plantilla", fileType);
        var year = timeProvider.GetUtcNow().Year;
        var content = templates.Generate(fileType, [year - 2, year - 1]);
        return File(content, XlsxContentType, fileName);
    }
}
