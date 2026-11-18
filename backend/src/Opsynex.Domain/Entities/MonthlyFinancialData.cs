using Opsynex.Domain.Common;

namespace Opsynex.Domain.Entities;

/// <summary>Ventas y compras de un mes (hoja de ventas mensuales). Único por empresa, año y mes.</summary>
public sealed class MonthlyFinancialData : Entity, ICompanyYearScoped
{
    public Guid CompanyId { get; set; }

    public int Year { get; set; }

    /// <summary>Mes, de 1 (enero) a 12 (diciembre).</summary>
    public int Month { get; set; }

    public decimal? Revenue { get; set; }

    public decimal? Purchases { get; set; }

    /// <summary>Carga de la que provienen los datos.</summary>
    public Guid? UploadId { get; set; }
}
