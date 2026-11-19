using Opsynex.Domain.Common;
using Opsynex.Domain.Enums;

namespace Opsynex.Domain.Entities;

/// <summary>
/// Homologación de una cuenta del cliente a una categoría estándar de Opsynex. Es único por
/// empresa, alcance, sección y cuenta, y se reutiliza en las cargas siguientes de la empresa.
/// </summary>
public sealed class AccountMapping : Entity, ICompanyScoped
{
    public Guid CompanyId { get; set; }

    /// <summary>Etiqueta normalizada de la cuenta (igual a <see cref="RawAccountValue.ClientAccount"/>).</summary>
    public required string ClientAccount { get; set; }

    public MappingScope Scope { get; set; }

    /// <summary>
    /// Sección normalizada; solo para subtotales de los estados ("Total corriente" de activos o de
    /// pasivos). Vacía en los demás casos.
    /// </summary>
    public string Section { get; set; } = string.Empty;

    public MappingRole Role { get; set; }

    public OpsynexCategory OpsynexCategory { get; set; }

    /// <summary>Confianza de la sugerencia (0 a 1); 1 en mapeos manuales.</summary>
    public decimal Confidence { get; set; }

    public MappingMethod Method { get; set; }

    /// <summary>Usuario que confirmó el mapeo; <c>null</c> si aún es una sugerencia.</summary>
    public Guid? ConfirmedBy { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public bool IsConfirmed => ConfirmedAt is not null;
}
