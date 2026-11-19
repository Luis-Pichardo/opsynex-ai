using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;

namespace Opsynex.Application.Homologation;

public sealed record MappingDto(
    Guid Id,
    string ClientAccount,
    MappingScope Scope,
    string Section,
    MappingRole Role,
    OpsynexCategory Category,
    decimal Confidence,
    MappingMethod Method,
    bool IsConfirmed,
    DateTime? ConfirmedAt,
    Guid? ConfirmedBy)
{
    public static MappingDto From(AccountMapping m) => new(
        m.Id, m.ClientAccount, m.Scope, m.Section, m.Role, m.OpsynexCategory, m.Confidence, m.Method, m.IsConfirmed, m.ConfirmedAt, m.ConfirmedBy);
}

/// <summary>Cuenta de una carga con su mapeo actual (para la revisión).</summary>
public sealed record PendingAccountDto(
    Guid MappingId,
    string ClientAccount,
    string OriginalLabel,
    string SheetName,
    SheetType SheetType,
    int RowNumber,
    string? Section,
    MappingScope Scope,
    MappingRole Role,
    OpsynexCategory Category,
    decimal Confidence,
    MappingMethod Method,
    bool IsConfirmed,
    bool IsDoubtful,
    int Year,
    decimal Amount);

public sealed record MappingUpdate(Guid Id, OpsynexCategory Category);

/// <summary>Guardar o confirmar mapeos (masivo). La categoría queda confirmada por el usuario.</summary>
public sealed record SaveMappingsRequest(IReadOnlyList<MappingUpdate> Items);

/// <summary>Resultado del análisis automático de una carga.</summary>
public sealed record ProcessingResult(
    Guid UploadId,
    UploadStatus Status,
    AnalysisStatus AnalysisStatus,
    bool NeedsReview,
    int DoubtfulAccounts,
    bool UsedFallback,
    IReadOnlyList<int> Years);

/// <summary>Respuesta de consolidar: estado del análisis y los datos financieros por año.</summary>
public sealed record ConsolidationResultDto(ProcessingResult Result, IReadOnlyList<FinancialData> FinancialData);
