using Opsynex.Domain.Common;

namespace Opsynex.Domain.Entities;

/// <summary>Datos operacionales anuales de una empresa.</summary>
public sealed class OperationalData : Entity, ICompanyYearScoped
{
    public Guid CompanyId { get; set; }

    public int Year { get; set; }

    public decimal? UnitsProduced { get; set; }

    public int? EmployeeCount { get; set; }

    /// <summary>Scrap estimado (valor monetario del desperdicio de material).</summary>
    public decimal? ScrapEstimate { get; set; }

    public decimal? EnergyCost { get; set; }

    public decimal? OperationalCosts { get; set; }

    public decimal? EnergyConsumptionKwh { get; set; }

    /// <summary>Carga de la que provienen los datos (si aplica).</summary>
    public Guid? UploadId { get; set; }
}
