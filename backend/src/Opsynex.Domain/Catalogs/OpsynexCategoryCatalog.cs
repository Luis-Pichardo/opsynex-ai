using Opsynex.Domain.Enums;

namespace Opsynex.Domain.Catalogs;

/// <summary>Estado financiero al que pertenece una categoría.</summary>
public enum FinancialStatement
{
    IncomeStatement,
    BalanceSheet,
    CashFlow,
    None,
}

/// <summary>Descripción de una categoría estándar.</summary>
public sealed record OpsynexCategoryInfo(
    OpsynexCategory Category,
    string Name,
    string Description,
    FinancialStatement Statement);

/// <summary>Catálogo de categorías estándar con nombre y descripción en español.</summary>
public static class OpsynexCategoryCatalog
{
    private static readonly OpsynexCategoryInfo[] Items =
    [
        new(OpsynexCategory.Revenue, "Ingresos", "Ventas netas de bienes y servicios.", FinancialStatement.IncomeStatement),
        new(OpsynexCategory.Cogs, "Costo de ventas", "Costo directo de los bienes vendidos o servicios prestados (COGS).", FinancialStatement.IncomeStatement),
        new(OpsynexCategory.OperatingExpenses, "Gastos operacionales", "Gastos de administración y ventas (OPEX).", FinancialStatement.IncomeStatement),
        new(OpsynexCategory.OverheadCost, "Gastos generales", "Costos indirectos o de estructura (overhead).", FinancialStatement.IncomeStatement),
        new(OpsynexCategory.EnergyCost, "Costo de energía", "Electricidad, gas, combustibles y otros energéticos.", FinancialStatement.IncomeStatement),
        new(OpsynexCategory.MaterialLoss, "Pérdida de materiales", "Mermas, desperdicio y scrap de materiales.", FinancialStatement.IncomeStatement),
        new(OpsynexCategory.MaintenanceCost, "Costo de mantenimiento", "Mantenimiento preventivo y correctivo de equipos e instalaciones.", FinancialStatement.IncomeStatement),
        new(OpsynexCategory.LaborCost, "Costo de personal", "Sueldos, salarios, cargas sociales y beneficios.", FinancialStatement.IncomeStatement),
        new(OpsynexCategory.Depreciation, "Depreciación y amortización", "Depreciación de activos fijos y amortización de intangibles.", FinancialStatement.IncomeStatement),
        new(OpsynexCategory.Interest, "Intereses", "Gastos financieros por intereses.", FinancialStatement.IncomeStatement),
        new(OpsynexCategory.Taxes, "Impuestos", "Impuesto a la renta o sobre utilidades.", FinancialStatement.IncomeStatement),
        new(OpsynexCategory.NetIncome, "Utilidad neta", "Resultado del ejercicio después de impuestos.", FinancialStatement.IncomeStatement),
        new(OpsynexCategory.TotalAssets, "Activos totales", "Total de activos del balance.", FinancialStatement.BalanceSheet),
        new(OpsynexCategory.TotalLiabilities, "Pasivos totales", "Total de pasivos del balance.", FinancialStatement.BalanceSheet),
        new(OpsynexCategory.Equity, "Patrimonio", "Capital y reservas de los accionistas.", FinancialStatement.BalanceSheet),
        new(OpsynexCategory.OperatingCashFlow, "Flujo de caja operativo", "Efectivo generado por las operaciones.", FinancialStatement.CashFlow),
        new(OpsynexCategory.Other, "Otros", "Cuentas que no corresponden a ninguna categoría estándar.", FinancialStatement.None),
    ];

    private static readonly Dictionary<OpsynexCategory, OpsynexCategoryInfo> ByCategory =
        Items.ToDictionary(i => i.Category);

    /// <summary>Todas las categorías, en el orden del enum.</summary>
    public static IReadOnlyList<OpsynexCategoryInfo> All => Items;

    /// <summary>Información de una categoría.</summary>
    public static OpsynexCategoryInfo Get(OpsynexCategory category) => ByCategory[category];
}
