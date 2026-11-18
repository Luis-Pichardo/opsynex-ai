namespace Opsynex.Domain.Engines.Kpis;

/// <summary>Datos de un año para calcular los KPIs (de FinancialData y OperationalData).</summary>
public sealed record KpiInput
{
    public decimal? Revenue { get; init; }

    public decimal? Cogs { get; init; }

    public decimal? TotalCost { get; init; }

    public decimal? OperatingExpenses { get; init; }

    public decimal? Ebitda { get; init; }

    public decimal? OverheadCost { get; init; }

    public decimal? EnergyCost { get; init; }

    public decimal? NetIncome { get; init; }

    public decimal? LaborCost { get; init; }

    public decimal? CurrentAssets { get; init; }

    public decimal? CurrentLiabilities { get; init; }

    public decimal? TotalLiabilities { get; init; }

    public decimal? Equity { get; init; }

    public decimal? AccountsReceivable { get; init; }

    public decimal? Inventory { get; init; }

    public int? EmployeeCount { get; init; }

    public decimal? UnitsProduced { get; init; }
}

/// <summary>
/// Motor de KPIs (clase pura). Toda división está protegida: el KPI es <c>null</c> si falta un
/// dato o si el denominador es 0 o negativo (ingresos, costo de ventas, empleados, unidades,
/// pasivos corrientes o patrimonio negativos no tienen una interpretación útil). Los numeradores
/// negativos sí se aceptan (margen negativo, pérdida). Ratios con 4 decimales; montos y días con 2.
/// </summary>
public static class KpiEngine
{
    private const decimal DaysPerYear = 365m;

    public static IReadOnlyDictionary<KpiCode, decimal?> Calculate(KpiInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new Dictionary<KpiCode, decimal?>
        {
            [KpiCode.EbitdaMargin] = Ratio(input.Ebitda, input.Revenue),
            [KpiCode.GrossMargin] = Ratio(input.Revenue - input.Cogs, input.Revenue),
            [KpiCode.CostEfficiency] = Ratio(input.TotalCost, input.Revenue),
            [KpiCode.RevenuePerEmployee] = Money(input.Revenue, input.EmployeeCount),
            [KpiCode.OperatingCostRatio] = Ratio(input.OperatingExpenses, input.Revenue),
            [KpiCode.OverheadRatio] = Ratio(input.OverheadCost, input.Revenue),
            [KpiCode.EnergyCostRatio] = Ratio(input.EnergyCost, input.Revenue),
            [KpiCode.CostPerUnit] = Money(input.TotalCost, input.UnitsProduced),
            [KpiCode.NetMargin] = Ratio(input.NetIncome, input.Revenue),
            [KpiCode.LaborCostRatio] = Ratio(input.LaborCost, input.Revenue),
            [KpiCode.CurrentRatio] = Ratio(input.CurrentAssets, input.CurrentLiabilities),
            [KpiCode.DebtToEquity] = Ratio(input.TotalLiabilities, input.Equity),
            [KpiCode.DaysSalesOutstanding] = Days(input.AccountsReceivable, input.Revenue),
            [KpiCode.InventoryDays] = Days(input.Inventory, input.Cogs),
        };
    }

    /// <summary>numerador / denominador, o <c>null</c> si falta un dato o el denominador no es positivo.</summary>
    public static decimal? SafeDivide(decimal? numerator, decimal? denominator) =>
        numerator is { } n && denominator is { } d && d > 0 ? n / d : null;

    private static decimal? Ratio(decimal? numerator, decimal? denominator) =>
        SafeDivide(numerator, denominator) is { } value ? decimal.Round(value, 4) : null;

    private static decimal? Money(decimal? numerator, decimal? denominator) =>
        SafeDivide(numerator, denominator) is { } value ? decimal.Round(value, 2) : null;

    private static decimal? Days(decimal? numerator, decimal? denominator) =>
        SafeDivide(numerator, denominator) is { } value ? decimal.Round(value * DaysPerYear, 2) : null;
}
