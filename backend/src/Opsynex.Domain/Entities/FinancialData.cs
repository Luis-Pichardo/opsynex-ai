using Opsynex.Domain.Common;

namespace Opsynex.Domain.Entities;

/// <summary>
/// Datos financieros anuales homologados de una empresa. Los montos son opcionales:
/// un valor <c>null</c> significa que el cliente no lo informó.
/// </summary>
public sealed class FinancialData : Entity, ICompanyYearScoped
{
    public Guid CompanyId { get; set; }

    public int Year { get; set; }

    public decimal? Revenue { get; set; }

    public decimal? Cogs { get; set; }

    public decimal? OperatingExpenses { get; set; }

    public decimal? Ebitda { get; set; }

    public decimal? TotalCost { get; set; }

    public decimal? OverheadCost { get; set; }

    public decimal? EnergyCost { get; set; }

    public decimal? MaintenanceCost { get; set; }

    public decimal? MaterialLoss { get; set; }

    public decimal? LaborCost { get; set; }

    public decimal? Depreciation { get; set; }

    public decimal? NetIncome { get; set; }

    public decimal? TotalAssets { get; set; }

    public decimal? TotalLiabilities { get; set; }

    public decimal? Equity { get; set; }

    public decimal? OperatingCashFlow { get; set; }
}
