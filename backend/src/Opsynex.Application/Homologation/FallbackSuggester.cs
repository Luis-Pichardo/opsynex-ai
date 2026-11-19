using Opsynex.Domain.Common;
using Opsynex.Domain.Enums;

namespace Opsynex.Application.Homologation;

/// <summary>
/// Diccionario mínimo local, para cuando el servicio de IA no responde: coincidencia exacta de
/// las etiquetas más comunes de los estados y las notas. Sus sugerencias se marcan
/// <c>Method = Fallback</c>.
/// </summary>
public static class FallbackSuggester
{
    public const decimal Confidence = 0.85m;

    private static readonly Dictionary<string, OpsynexCategory> Statement = Build(
        (OpsynexCategory.GrossSales, ["ventas netas", "ventas", "ventas brutas"]),
        (OpsynexCategory.SalesDiscounts, ["descuentos sobre ventas", "descuentos en ventas"]),
        (OpsynexCategory.Revenue, ["ingresos", "total ingresos"]),
        (OpsynexCategory.Cogs, ["costo de ventas", "costos de ventas"]),
        (OpsynexCategory.OperatingExpenses, ["compensaciones al personal", "gastos administrativos", "total gastos de operaciones", "gastos de operacion"]),
        (OpsynexCategory.Depreciation, ["depreciacion y amortizacion", "depreciacion"]),
        (OpsynexCategory.FinancialExpenses, ["gastos financieros", "intereses"]),
        (OpsynexCategory.EarningsBeforeTax, ["ingresos netos antes impuesto s renta", "ganancia antes de impuesto", "utilidad antes de impuesto"]),
        (OpsynexCategory.IncomeTax, ["impuesto sobre la renta", "isr"]),
        (OpsynexCategory.NetIncome, ["ingresos netos", "utilidad neta", "ganancia neta"]),
        (OpsynexCategory.Cash, ["efectivo en cajas y bancos", "efectivo", "caja y bancos"]),
        (OpsynexCategory.AccountsReceivable, ["cuentas por cobrar"]),
        (OpsynexCategory.Inventory, ["inventario productos terminados", "inventario materia prima", "inventarios", "inventario"]),
        (OpsynexCategory.TotalAssets, ["total activos", "total activo"]),
        (OpsynexCategory.AccountsPayable, ["cuentas por pagar"]),
        (OpsynexCategory.FinancialDebt, ["prestamos por pagar", "prestamos a largo plazo"]),
        (OpsynexCategory.Equity, ["total capital", "capital social", "reserva legal", "beneficios acumulados", "beneficio del periodo"]),
        (OpsynexCategory.TotalLiabilitiesAndEquity, ["total pasivos y capital", "total pasivos y patrimonio"]),
        (OpsynexCategory.Other, ["ganancia bruta", "ganancia en operaciones", "subtotal sin etiqueta", "gastos pagados por anticipado", "otros activos",
            "propiedad planta y equipo neto", "propiedad planta y equipo", "acumulaciones y retenciones por pagar"]));

    private static readonly Dictionary<string, OpsynexCategory> Notes = Build(
        (OpsynexCategory.EnergyCost, ["energia electrica", "energia"]),
        (OpsynexCategory.MaintenanceCost, ["reparaciones", "mantenimientos", "mantenimiento", "reparaciones y mantenimientos", "reparaciones y mantenimiento vehiculos"]),
        (OpsynexCategory.LaborCost, ["total compensaciones al personal", "total labor directa"]),
        (OpsynexCategory.RawMaterials, ["materias primas importadas", "compras de materias primas locales"]),
        (OpsynexCategory.OverheadCost, ["total gastos generales y administrativos"]),
        (OpsynexCategory.Depreciation, ["depreciacion maquinarias y equipos"]));

    public static OpsynexCategory? Find(AccountKey key, string normalizedLabel)
    {
        ArgumentNullException.ThrowIfNull(key);
        var text = TextKey.Normalize(normalizedLabel);
        if (key.Scope == MappingScope.Statement && text is "total corriente" or "total circulante")
        {
            return key.Section.Contains("pasivo", StringComparison.Ordinal) ? OpsynexCategory.CurrentLiabilities
                : key.Section.Contains("activo", StringComparison.Ordinal) ? OpsynexCategory.CurrentAssets
                : null;
        }

        var dictionary = key.Scope == MappingScope.Statement ? Statement : Notes;
        return dictionary.TryGetValue(text, out var category) ? category : null;
    }

    private static Dictionary<string, OpsynexCategory> Build(params (OpsynexCategory Category, string[] Terms)[] entries) =>
        entries.SelectMany(e => e.Terms.Select(t => (Term: t, e.Category))).ToDictionary(x => x.Term, x => x.Category, StringComparer.Ordinal);
}
