namespace Opsynex.Domain.Engines.Kpis;

/// <summary>Indicadores que calcula el motor de KPIs.</summary>
public enum KpiCode
{
    EbitdaMargin,
    GrossMargin,
    CostEfficiency,
    RevenuePerEmployee,
    OperatingCostRatio,
    OverheadRatio,
    EnergyCostRatio,
    CostPerUnit,
    NetMargin,
    LaborCostRatio,
    CurrentRatio,
    DebtToEquity,
    DaysSalesOutstanding,
    InventoryDays,
}

/// <summary>Unidad del KPI (define cómo se formatea y cómo se normaliza).</summary>
public enum KpiUnit
{
    /// <summary>Proporción (0,1235 = 12,35 %).</summary>
    Ratio,

    /// <summary>Veces (p. ej. razón corriente 1,8×).</summary>
    Times,

    /// <summary>Monto en la moneda de la empresa (sus benchmarks dependen de la moneda).</summary>
    Money,

    Days,
}

/// <summary>Dimensión del score a la que aporta el KPI (etapa 07).</summary>
public enum KpiDimension
{
    Profitability,
    CostEfficiency,
    Productivity,
    FinancialStability,
}

/// <summary>Definición de un KPI.</summary>
public sealed record KpiDefinition(
    KpiCode Code,
    string Name,
    string Formula,
    KpiUnit Unit,
    bool HigherIsBetter,
    KpiDimension Dimension,
    string Description);

/// <summary>Catálogo de KPIs (secciones 8.3 y 9 de las especificaciones).</summary>
public static class KpiCatalog
{
    public static readonly IReadOnlyList<KpiDefinition> All =
    [
        new(KpiCode.EbitdaMargin, "Margen EBITDA", "EBITDA / Revenue", KpiUnit.Ratio, true, KpiDimension.Profitability,
            "Rentabilidad operacional antes de intereses, impuestos, depreciación y amortización."),
        new(KpiCode.GrossMargin, "Margen bruto", "(Revenue − COGS) / Revenue", KpiUnit.Ratio, true, KpiDimension.Profitability,
            "Rentabilidad de la producción o del servicio."),
        new(KpiCode.CostEfficiency, "Eficiencia de costos", "Total Cost / Revenue", KpiUnit.Ratio, false, KpiDimension.CostEfficiency,
            "Proporción del costo total sobre los ingresos: bajar es favorable."),
        new(KpiCode.RevenuePerEmployee, "Ingresos por empleado", "Revenue / Number of Employees", KpiUnit.Money, true, KpiDimension.Productivity,
            "Productividad laboral."),
        new(KpiCode.OperatingCostRatio, "Gastos operacionales / ingresos", "Operating Expenses / Revenue", KpiUnit.Ratio, false, KpiDimension.CostEfficiency,
            "Carga de los gastos operacionales."),
        new(KpiCode.OverheadRatio, "Gastos generales / ingresos", "Overhead Cost / Revenue", KpiUnit.Ratio, false, KpiDimension.CostEfficiency,
            "Peso de los gastos generales y administrativos (auxiliar del diagnóstico)."),
        new(KpiCode.EnergyCostRatio, "Energía / ingresos", "Energy Cost / Revenue", KpiUnit.Ratio, false, KpiDimension.CostEfficiency,
            "Peso del costo de energía (auxiliar del diagnóstico)."),
        new(KpiCode.CostPerUnit, "Costo por unidad", "Total Cost / Units Produced", KpiUnit.Money, false, KpiDimension.Productivity,
            "Costo total por unidad producida (requiere datos operacionales)."),
        new(KpiCode.NetMargin, "Margen neto", "Net Income / Revenue", KpiUnit.Ratio, true, KpiDimension.Profitability,
            "Utilidad neta sobre los ingresos."),
        new(KpiCode.LaborCostRatio, "Costo de personal / ingresos", "Labor Cost / Revenue", KpiUnit.Ratio, false, KpiDimension.CostEfficiency,
            "Peso del costo de personal (administrativo y de producción)."),
        new(KpiCode.CurrentRatio, "Razón corriente", "Current Assets / Current Liabilities", KpiUnit.Times, true, KpiDimension.FinancialStability,
            "Capacidad de pagar las obligaciones de corto plazo."),
        new(KpiCode.DebtToEquity, "Endeudamiento", "Total Liabilities / Equity", KpiUnit.Times, false, KpiDimension.FinancialStability,
            "Pasivos totales por cada unidad de patrimonio."),
        new(KpiCode.DaysSalesOutstanding, "Días de cobro", "Accounts Receivable / Revenue × 365", KpiUnit.Days, false, KpiDimension.FinancialStability,
            "Días promedio que tarda en cobrarse una venta."),
        new(KpiCode.InventoryDays, "Días de inventario", "Inventory / COGS × 365", KpiUnit.Days, false, KpiDimension.CostEfficiency,
            "Días de costo de ventas cubiertos por el inventario."),
    ];

    private static readonly Dictionary<KpiCode, KpiDefinition> ByCode = All.ToDictionary(d => d.Code);

    public static KpiDefinition Get(KpiCode code) => ByCode[code];
}
