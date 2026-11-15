using Opsynex.Domain.Common;

namespace Opsynex.Domain.Entities;

/// <summary>KPIs calculados de una empresa para un año. Un KPI es <c>null</c> si no se puede calcular.</summary>
public sealed class KpiResult : Entity, ICompanyYearScoped
{
    public Guid CompanyId { get; set; }

    public int Year { get; set; }

    /// <summary>EBITDA / Revenue.</summary>
    public decimal? EbitdaMargin { get; set; }

    /// <summary>(Revenue − COGS) / Revenue.</summary>
    public decimal? GrossMargin { get; set; }

    /// <summary>Total cost / Revenue.</summary>
    public decimal? CostEfficiency { get; set; }

    /// <summary>Revenue / número de empleados (monto).</summary>
    public decimal? RevenuePerEmployee { get; set; }

    /// <summary>Operating expenses / Revenue.</summary>
    public decimal? OperatingCostRatio { get; set; }

    public DateTime CalculatedAt { get; set; }
}
