using Opsynex.Domain.Common;
using Opsynex.Domain.Enums;

namespace Opsynex.Domain.Entities;

/// <summary>Archivo Excel/CSV cargado por un usuario.</summary>
public sealed class Upload : Entity, ICompanyScoped
{
    public Guid CompanyId { get; set; }

    public Guid UserId { get; set; }

    public required string FileName { get; set; }

    public UploadFileType FileType { get; set; }

    public UploadStatus Status { get; set; } = UploadStatus.Pending;

    /// <summary>Errores de validación (columna JSON).</summary>
    public IList<UploadError> Errors { get; set; } = [];

    public int RowsCount { get; set; }

    public ICollection<RawAccountValue> RawValues { get; } = [];
}

/// <summary>Error de validación de una carga (fila y columna opcionales).</summary>
public sealed record UploadError(string Code, string Message, int? Row = null, string? Column = null);
