using Opsynex.Domain.Common;
using Opsynex.Domain.Enums;

namespace Opsynex.Domain.Entities;

/// <summary>Homologación de una cuenta del cliente a una categoría estándar de Opsynex.</summary>
public sealed class AccountMapping : Entity, ICompanyScoped
{
    public Guid CompanyId { get; set; }

    public required string ClientAccount { get; set; }

    public OpsynexCategory OpsynexCategory { get; set; }

    /// <summary>Confianza de la sugerencia (0 a 1); 1 en mapeos manuales.</summary>
    public decimal Confidence { get; set; }

    public MappingMethod Method { get; set; }

    /// <summary>Usuario que confirmó el mapeo; <c>null</c> si aún es una sugerencia.</summary>
    public Guid? ConfirmedBy { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public bool IsConfirmed => ConfirmedAt is not null;
}
