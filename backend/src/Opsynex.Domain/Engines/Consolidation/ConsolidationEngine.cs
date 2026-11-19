using Opsynex.Domain.Enums;

namespace Opsynex.Domain.Engines.Consolidation;

/// <summary>Valor en bruto ya homologado (una fila de un año).</summary>
public sealed record ConsolidationRow(int Year, OpsynexCategory Category, MappingRole Role, RowKind RowKind, decimal Value);

/// <summary>Valores consolidados de un año por categoría (incluye los derivados).</summary>
public sealed class ConsolidatedYear(int year)
{
    private readonly Dictionary<OpsynexCategory, decimal> _values = [];

    public int Year { get; } = year;

    public IReadOnlyDictionary<OpsynexCategory, decimal> Values => _values;

    public decimal? this[OpsynexCategory category] => _values.TryGetValue(category, out var value) ? value : null;

    /// <summary>Costo total = costo de ventas + gastos operacionales (derivado).</summary>
    public decimal? TotalCost { get; internal set; }

    /// <summary>EBITDA = ganancia antes de ISR + gastos financieros + depreciación (derivado).</summary>
    public decimal? Ebitda { get; internal set; }

    internal void Set(OpsynexCategory category, decimal value) => _values[category] = value;

    internal bool Has(OpsynexCategory category) => _values.ContainsKey(category);
}

/// <summary>
/// Consolida los valores en bruto homologados por categoría y año, sin doble conteo
/// (motor puro, sin EF ni HTTP):
/// <list type="number">
/// <item>Categorías de los estados (ingresos, costos, activos…): solo filas <c>Primary</c>.</item>
/// <item>Categorías analíticas (energía, mantenimiento, personal, materias primas, gastos generales,
/// merma): filas <c>Breakdown</c> de las notas; si no hay, las <c>Primary</c>.</item>
/// <item>Depreciación: la de gastos (<c>Primary</c>) más la incluida en el costo (<c>Breakdown</c>).</item>
/// <item>En cada grupo, si hay subtotales o totales mapeados a la categoría se usan ellos; si no,
/// se suman los detalles. Nunca se suman ambos.</item>
/// <item>Derivados cuando no vienen: Revenue = ventas − descuentos; TotalCost = Cogs + Opex;
/// Ebitda = antes de ISR + gastos financieros + depreciación; TotalLiabilities = total pasivos y
/// capital − capital.</item>
/// </list>
/// </summary>
public static class ConsolidationEngine
{
    /// <summary>Categorías que se calculan con el detalle de las notas.</summary>
    public static readonly IReadOnlySet<OpsynexCategory> AnalyticCategories = new HashSet<OpsynexCategory>
    {
        OpsynexCategory.EnergyCost,
        OpsynexCategory.MaintenanceCost,
        OpsynexCategory.LaborCost,
        OpsynexCategory.RawMaterials,
        OpsynexCategory.OverheadCost,
        OpsynexCategory.MaterialLoss,
    };

    public static IReadOnlyList<ConsolidatedYear> Consolidate(IEnumerable<ConsolidationRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var result = new List<ConsolidatedYear>();
        foreach (var yearRows in rows.Where(r => r.Category != OpsynexCategory.Other).GroupBy(r => r.Year).OrderBy(g => g.Key))
        {
            var year = new ConsolidatedYear(yearRows.Key);
            foreach (var categoryRows in yearRows.GroupBy(r => r.Category))
            {
                if (ValueFor(categoryRows.Key, categoryRows.ToList()) is { } value)
                {
                    year.Set(categoryRows.Key, value);
                }
            }

            ApplyEquivalents(year);
            ApplyDerived(year);
            result.Add(year);
        }

        return result;
    }

    private static decimal? ValueFor(OpsynexCategory category, List<ConsolidationRow> rows)
    {
        var primary = Aggregate(rows.Where(r => r.Role == MappingRole.Primary));
        var breakdown = Aggregate(rows.Where(r => r.Role == MappingRole.Breakdown));

        if (category == OpsynexCategory.Depreciation)
        {
            return primary is null && breakdown is null ? null : (primary ?? 0) + (breakdown ?? 0);
        }

        return AnalyticCategories.Contains(category) ? breakdown ?? primary : primary;
    }

    /// <summary>Subtotales o totales si los hay; si no, la suma de los detalles.</summary>
    private static decimal? Aggregate(IEnumerable<ConsolidationRow> rows)
    {
        var list = rows.ToList();
        if (list.Count == 0)
        {
            return null;
        }

        var totals = list.Where(r => r.RowKind != RowKind.Detail).ToList();
        return totals.Count > 0 ? totals.Sum(r => r.Value) : list.Sum(r => r.Value);
    }

    /// <summary>Categorías antiguas equivalentes (Interest, Taxes) si faltan las del libro dominicano.</summary>
    private static void ApplyEquivalents(ConsolidatedYear year)
    {
        if (!year.Has(OpsynexCategory.FinancialExpenses) && year[OpsynexCategory.Interest] is { } interest)
        {
            year.Set(OpsynexCategory.FinancialExpenses, interest);
        }

        if (!year.Has(OpsynexCategory.IncomeTax) && year[OpsynexCategory.Taxes] is { } taxes)
        {
            year.Set(OpsynexCategory.IncomeTax, taxes);
        }
    }

    private static void ApplyDerived(ConsolidatedYear year)
    {
        if (!year.Has(OpsynexCategory.Revenue) && year[OpsynexCategory.GrossSales] is { } gross)
        {
            year.Set(OpsynexCategory.Revenue, gross - (year[OpsynexCategory.SalesDiscounts] ?? 0));
        }

        if (year[OpsynexCategory.Cogs] is { } cogs && year[OpsynexCategory.OperatingExpenses] is { } opex)
        {
            year.TotalCost = cogs + opex;
        }

        if (!year.Has(OpsynexCategory.EarningsBeforeTax)
            && year[OpsynexCategory.NetIncome] is { } net && year[OpsynexCategory.IncomeTax] is { } tax)
        {
            year.Set(OpsynexCategory.EarningsBeforeTax, net + tax);
        }

        if (year[OpsynexCategory.EarningsBeforeTax] is { } ebt)
        {
            year.Ebitda = ebt + (year[OpsynexCategory.FinancialExpenses] ?? 0) + (year[OpsynexCategory.Depreciation] ?? 0);
        }

        if (!year.Has(OpsynexCategory.TotalLiabilities)
            && year[OpsynexCategory.TotalLiabilitiesAndEquity] is { } total && year[OpsynexCategory.Equity] is { } equity)
        {
            year.Set(OpsynexCategory.TotalLiabilities, total - equity);
        }
    }
}
