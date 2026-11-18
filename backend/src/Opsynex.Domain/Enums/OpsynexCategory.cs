namespace Opsynex.Domain.Enums;

/// <summary>
/// Categorías estándar de Opsynex a las que se homologan las cuentas del cliente.
/// Las descripciones en español están en <see cref="Catalogs.OpsynexCategoryCatalog"/>.
/// </summary>
public enum OpsynexCategory
{
    Revenue,
    Cogs,
    OperatingExpenses,
    OverheadCost,
    EnergyCost,
    MaterialLoss,
    MaintenanceCost,
    LaborCost,
    Depreciation,
    Interest,
    Taxes,
    NetIncome,
    TotalAssets,
    TotalLiabilities,
    Equity,
    OperatingCashFlow,
    Other,

    // Categorías del libro contable dominicano (FORMATO_EXCEL_ENTRADA.md, sección 4).
    GrossSales,
    SalesDiscounts,
    FinancialExpenses,
    EarningsBeforeTax,
    IncomeTax,
    RawMaterials,
    Cash,
    AccountsReceivable,
    Inventory,
    CurrentAssets,
    CurrentLiabilities,
    AccountsPayable,
    FinancialDebt,
}
