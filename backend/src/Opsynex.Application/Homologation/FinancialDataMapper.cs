using Opsynex.Domain.Engines.Consolidation;
using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;

namespace Opsynex.Application.Homologation;

/// <summary>
/// Pasa los valores consolidados de un año a <see cref="FinancialData"/>. Solo sobrescribe los
/// grupos de campos que la carga trae (resultados, balance o flujo), para que una plantilla de un
/// solo estado no borre los datos del otro.
/// </summary>
public static class FinancialDataMapper
{
    public static void Apply(FinancialData data, ConsolidatedYear year, bool income, bool balance, bool cashFlow)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(year);
        if (income)
        {
            data.Revenue = year[OpsynexCategory.Revenue];
            data.GrossSales = year[OpsynexCategory.GrossSales];
            data.SalesDiscounts = year[OpsynexCategory.SalesDiscounts];
            data.Cogs = year[OpsynexCategory.Cogs];
            data.OperatingExpenses = year[OpsynexCategory.OperatingExpenses];
            data.TotalCost = year.TotalCost;
            data.Ebitda = year.Ebitda;
            data.OverheadCost = year[OpsynexCategory.OverheadCost];
            data.EnergyCost = year[OpsynexCategory.EnergyCost];
            data.MaintenanceCost = year[OpsynexCategory.MaintenanceCost];
            data.MaterialLoss = year[OpsynexCategory.MaterialLoss];
            data.LaborCost = year[OpsynexCategory.LaborCost];
            data.Depreciation = year[OpsynexCategory.Depreciation];
            data.FinancialExpenses = year[OpsynexCategory.FinancialExpenses];
            data.EarningsBeforeTax = year[OpsynexCategory.EarningsBeforeTax];
            data.IncomeTax = year[OpsynexCategory.IncomeTax];
            data.NetIncome = year[OpsynexCategory.NetIncome];
            data.RawMaterials = year[OpsynexCategory.RawMaterials];
        }

        if (balance)
        {
            data.TotalAssets = year[OpsynexCategory.TotalAssets];
            data.TotalLiabilities = year[OpsynexCategory.TotalLiabilities];
            data.Equity = year[OpsynexCategory.Equity];
            data.Cash = year[OpsynexCategory.Cash];
            data.AccountsReceivable = year[OpsynexCategory.AccountsReceivable];
            data.Inventory = year[OpsynexCategory.Inventory];
            data.CurrentAssets = year[OpsynexCategory.CurrentAssets];
            data.CurrentLiabilities = year[OpsynexCategory.CurrentLiabilities];
            data.AccountsPayable = year[OpsynexCategory.AccountsPayable];
            data.FinancialDebt = year[OpsynexCategory.FinancialDebt];
        }

        if (cashFlow)
        {
            data.OperatingCashFlow = year[OpsynexCategory.OperatingCashFlow];
        }
    }
}
