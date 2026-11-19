using Opsynex.Domain.Common;
using Opsynex.Domain.Enums;

namespace Opsynex.Domain.Entities;

/// <summary>
/// Valor de una fila de un estado financiero para un año, tal como vino en el archivo (antes de
/// homologar). Las etiquetas pasan por un filtro que oculta cédulas, RNC y números de cuenta.
/// </summary>
public sealed class RawAccountValue : Entity, ICompanyYearScoped
{
    public Guid UploadId { get; set; }

    public Guid CompanyId { get; set; }

    /// <summary>Cuenta del cliente usada para homologar (la etiqueta normalizada).</summary>
    public required string ClientAccount { get; set; }

    public int Year { get; set; }

    public decimal Value { get; set; }

    public string SheetName { get; set; } = string.Empty;

    public SheetType SheetType { get; set; }

    /// <summary>Fila de Excel (1 = primera fila).</summary>
    public int RowNumber { get; set; }

    /// <summary>Sección del estado (p. ej. "ACTIVOS CORRIENTES" o el título de la nota).</summary>
    public string? Section { get; set; }

    public string OriginalLabel { get; set; } = string.Empty;

    /// <summary>Etiqueta sin "(nota N)", "Menos:", "más:" ni espacios extra.</summary>
    public string NormalizedLabel { get; set; } = string.Empty;

    /// <summary>Código de cuenta (balance de comprobación).</summary>
    public string? AccountCode { get; set; }

    public RowKind RowKind { get; set; }
}
