using Opsynex.Domain.Common;

namespace Opsynex.Domain.Entities;

/// <summary>Valor de una cuenta del cliente para un año, tal como vino en el archivo.</summary>
public sealed class RawAccountValue : Entity, ICompanyYearScoped
{
    public Guid UploadId { get; set; }

    public Guid CompanyId { get; set; }

    /// <summary>Nombre de la cuenta en el plan contable del cliente.</summary>
    public required string ClientAccount { get; set; }

    public int Year { get; set; }

    public decimal Value { get; set; }
}
