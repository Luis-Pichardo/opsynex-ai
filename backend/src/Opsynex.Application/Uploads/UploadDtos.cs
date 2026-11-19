using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;

namespace Opsynex.Application.Uploads;

/// <summary>Archivo recibido por el endpoint de carga.</summary>
public sealed record UploadFileRequest(Stream Content, string FileName, long Length, Guid? CompanyId, UploadFileType? FileType);

/// <summary>Reporte completo de una carga.</summary>
public sealed record UploadReportDto(
    Guid UploadId,
    Guid CompanyId,
    string FileName,
    UploadFileType FileType,
    UploadStatus Status,
    IReadOnlyList<int> Years,
    IReadOnlyList<UploadSheet> Sheets,
    IReadOnlyList<UploadIssue> Errors,
    IReadOnlyList<UploadIssue> Warnings,
    UploadSummary Summary,
    int RowsCount,
    UploadPreview Preview,
    DateTime CreatedAt,
    AnalysisStatus AnalysisStatus,
    bool NeedsReview,
    int DoubtfulAccounts);

/// <summary>Primeras líneas del estado de resultados y del balance (o de la plantilla).</summary>
public sealed record UploadPreview(IReadOnlyList<PreviewLine> Lines);

public sealed record PreviewLine(
    string Sheet,
    SheetType SheetType,
    int Row,
    string? Section,
    string Label,
    RowKind RowKind,
    IReadOnlyDictionary<int, decimal> Values);

/// <summary>Carga en el listado.</summary>
public sealed record UploadListItemDto(
    Guid Id,
    Guid CompanyId,
    string FileName,
    UploadFileType FileType,
    UploadStatus Status,
    IReadOnlyList<int> Years,
    int RowsCount,
    int WarningsCount,
    int ErrorsCount,
    DateTime CreatedAt,
    AnalysisStatus AnalysisStatus,
    bool NeedsReview);
