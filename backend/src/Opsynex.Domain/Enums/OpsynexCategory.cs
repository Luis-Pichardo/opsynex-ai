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
}
