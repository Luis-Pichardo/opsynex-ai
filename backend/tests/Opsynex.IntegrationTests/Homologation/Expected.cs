using System.Globalization;
using System.Text.Json;
using Opsynex.Domain.Entities;
using Opsynex.IntegrationTests.Uploads;

namespace Opsynex.IntegrationTests.Homologation;

/// <summary>Valores esperados del generador de libros sintéticos (expected_values.json).</summary>
internal static class Expected
{
    private static readonly JsonDocument Manifest =
        JsonDocument.Parse(File.ReadAllText(Path.Combine(UploadClient.SampleDirectory, "expected_values.json")));

    public static decimal V(string file, int year, string key) =>
        Manifest.RootElement.GetProperty(file).GetProperty("values").GetProperty(year.ToString(CultureInfo.InvariantCulture)).GetProperty(key).GetDecimal();

    public static decimal Sum(string file, int year, params string[] keys) => keys.Sum(k => V(file, year, k));

    /// <summary>FinancialData esperado de un año del libro sintético (sección 4 de la especificación).</summary>
    public static IReadOnlyList<(string Field, decimal? Actual, decimal Expected)> Compare(FinancialData data, string file, int year) =>
    [
        ("Revenue", data.Revenue, V(file, year, "net_sales")),
        ("GrossSales", data.GrossSales, V(file, year, "gross_sales")),
        ("SalesDiscounts", data.SalesDiscounts, V(file, year, "discounts")),
        ("Cogs", data.Cogs, V(file, year, "cogs")),
        ("OperatingExpenses", data.OperatingExpenses, V(file, year, "opex_total")),
        ("TotalCost", data.TotalCost, Sum(file, year, "cogs", "opex_total")),
        ("FinancialExpenses", data.FinancialExpenses, V(file, year, "fin_exp")),
        ("EarningsBeforeTax", data.EarningsBeforeTax, V(file, year, "ebt")),
        ("IncomeTax", data.IncomeTax, V(file, year, "isr")),
        ("NetIncome", data.NetIncome, V(file, year, "net_income")),
        ("Depreciation", data.Depreciation, Sum(file, year, "dep_amort", "fab_dep")),
        ("Ebitda", data.Ebitda, V(file, year, "ebitda")),
        ("EnergyCost", data.EnergyCost, Sum(file, year, "g_energia", "fab_energia")),
        ("MaintenanceCost", data.MaintenanceCost, Sum(file, year, "g_rep", "g_mant", "g_rep_veh", "fab_mant")),
        ("LaborCost", data.LaborCost, Sum(file, year, "comp_total", "labor_total")),
        ("OverheadCost", data.OverheadCost, V(file, year, "admin_total")),
        ("RawMaterials", data.RawMaterials, Sum(file, year, "mp_import", "mp_local")),
        ("Cash", data.Cash, V(file, year, "cash")),
        ("AccountsReceivable", data.AccountsReceivable, V(file, year, "cxc_total")),
        ("Inventory", data.Inventory, V(file, year, "inventory")),
        ("CurrentAssets", data.CurrentAssets, V(file, year, "current_assets")),
        ("TotalAssets", data.TotalAssets, V(file, year, "total_assets")),
        ("AccountsPayable", data.AccountsPayable, V(file, year, "cxp_total")),
        ("CurrentLiabilities", data.CurrentLiabilities, V(file, year, "pasivo_corriente")),
        ("FinancialDebt", data.FinancialDebt, V(file, year, "financial_debt")),
        ("TotalLiabilities", data.TotalLiabilities, V(file, year, "total_liabilities")),
        ("Equity", data.Equity, V(file, year, "equity")),
    ];
}
