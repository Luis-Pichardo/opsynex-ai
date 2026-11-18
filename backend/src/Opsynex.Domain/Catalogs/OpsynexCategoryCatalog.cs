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
        new(OpsynexCategory.GrossSales, "Ventas brutas", "Ventas antes de descuentos (\"Ventas netas\" en el libro dominicano).", FinancialStatement.IncomeStatement),
        new(OpsynexCategory.SalesDiscounts, "Descuentos sobre ventas", "Descuentos, devoluciones y bonificaciones sobre ventas.", FinancialStatement.IncomeStatement),
        new(OpsynexCategory.FinancialExpenses, "Gastos financieros", "Intereses y comisiones bancarias; no forman parte del EBITDA.", FinancialStatement.IncomeStatement),
        new(OpsynexCategory.EarningsBeforeTax, "Ganancia antes de impuestos", "Resultado antes del impuesto sobre la renta (ISR).", FinancialStatement.IncomeStatement),
        new(OpsynexCategory.IncomeTax, "Impuesto sobre la renta", "ISR del período.", FinancialStatement.IncomeStatement),
        new(OpsynexCategory.RawMaterials, "Materias primas", "Compras de materias primas locales e importadas.", FinancialStatement.IncomeStatement),
        new(OpsynexCategory.Cash, "Efectivo", "Efectivo en cajas y bancos.", FinancialStatement.BalanceSheet),
        new(OpsynexCategory.AccountsReceivable, "Cuentas por cobrar", "Cuentas por cobrar netas de provisión.", FinancialStatement.BalanceSheet),
        new(OpsynexCategory.Inventory, "Inventarios", "Inventarios de productos terminados y materias primas.", FinancialStatement.BalanceSheet),
        new(OpsynexCategory.CurrentAssets, "Activos corrientes", "Total de activos corrientes.", FinancialStatement.BalanceSheet),
        new(OpsynexCategory.CurrentLiabilities, "Pasivos corrientes", "Total de pasivos corrientes.", FinancialStatement.BalanceSheet),
        new(OpsynexCategory.AccountsPayable, "Cuentas por pagar", "Cuentas por pagar a proveedores.", FinancialStatement.BalanceSheet),
        new(OpsynexCategory.FinancialDebt, "Deuda financiera", "Préstamos bancarios de corto y largo plazo.", FinancialStatement.BalanceSheet),
    ];

    private static readonly Dictionary<OpsynexCategory, OpsynexCategoryInfo> ByCategory =
        Items.ToDictionary(i => i.Category);

    /// <summary>Todas las categorías, en el orden del enum.</summary>
    public static IReadOnlyList<OpsynexCategoryInfo> All => Items;

    /// <summary>Información de una categoría.</summary>
    public static OpsynexCategoryInfo Get(OpsynexCategory category) => ByCategory[category];
}
