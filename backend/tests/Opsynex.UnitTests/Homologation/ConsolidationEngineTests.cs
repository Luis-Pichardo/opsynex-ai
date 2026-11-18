using Opsynex.Domain.Engines.Consolidation;
using Opsynex.Domain.Enums;

namespace Opsynex.UnitTests.Homologation;

public sealed class ConsolidationEngineTests
{
    private const int Y = 2024;

    private static ConsolidationRow Row(OpsynexCategory category, decimal value, MappingRole role = MappingRole.Primary, RowKind kind = RowKind.Detail) =>
        new(Y, category, role, kind, value);

    private static ConsolidatedYear Consolidate(params ConsolidationRow[] rows) => ConsolidationEngine.Consolidate(rows).Single();

    [Fact]
    public void Subtotals_ReplaceDetails_NeverBothSummed()
    {
        var year = Consolidate(
            Row(OpsynexCategory.OperatingExpenses, 100),
            Row(OpsynexCategory.OperatingExpenses, 50),
            Row(OpsynexCategory.OperatingExpenses, 150, kind: RowKind.Subtotal));

        year[OpsynexCategory.OperatingExpenses].ShouldBe(150);
    }

    [Fact]
    public void WithoutSubtotals_DetailsAreSummed()
    {
        Consolidate(Row(OpsynexCategory.Inventory, 70), Row(OpsynexCategory.Inventory, 30))[OpsynexCategory.Inventory].ShouldBe(100);
    }

    [Fact]
    public void StatementCategories_IgnoreNoteBreakdown()
    {
        var year = Consolidate(
            Row(OpsynexCategory.Cogs, 1_000),
            Row(OpsynexCategory.Cogs, 400, MappingRole.Breakdown),
            Row(OpsynexCategory.Cash, 90),
            Row(OpsynexCategory.Cash, 90, MappingRole.Breakdown));

        year[OpsynexCategory.Cogs].ShouldBe(1_000);
        year[OpsynexCategory.Cash].ShouldBe(90);
    }

    [Fact]
    public void AnalyticCategories_UseNotesAndFallBackToStatements()
    {
        var withNotes = Consolidate(
            Row(OpsynexCategory.EnergyCost, 10, MappingRole.Breakdown),
            Row(OpsynexCategory.EnergyCost, 25, MappingRole.Breakdown),
            Row(OpsynexCategory.EnergyCost, 999));
        var onlyStatement = Consolidate(Row(OpsynexCategory.EnergyCost, 40));

        withNotes[OpsynexCategory.EnergyCost].ShouldBe(35);
        onlyStatement[OpsynexCategory.EnergyCost].ShouldBe(40);
    }

    [Fact]
    public void LaborCost_UsesNoteTotalsInsteadOfTheirDetails()
    {
        var year = Consolidate(
            Row(OpsynexCategory.LaborCost, 60, MappingRole.Breakdown),
            Row(OpsynexCategory.LaborCost, 40, MappingRole.Breakdown),
            Row(OpsynexCategory.LaborCost, 100, MappingRole.Breakdown, RowKind.Subtotal),
            Row(OpsynexCategory.LaborCost, 30, MappingRole.Breakdown),
            Row(OpsynexCategory.LaborCost, 30, MappingRole.Breakdown, RowKind.Subtotal));

        year[OpsynexCategory.LaborCost].ShouldBe(130);
    }

    [Fact]
    public void Depreciation_IsExpensesPlusCostOfProduction()
    {
        Consolidate(Row(OpsynexCategory.Depreciation, 20), Row(OpsynexCategory.Depreciation, 45, MappingRole.Breakdown))
            [OpsynexCategory.Depreciation].ShouldBe(65);
    }

    [Fact]
    public void Derived_RevenueTotalCostEbitdaAndLiabilities()
    {
        var year = Consolidate(
            Row(OpsynexCategory.GrossSales, 1_000),
            Row(OpsynexCategory.SalesDiscounts, 50),
            Row(OpsynexCategory.Cogs, 600),
            Row(OpsynexCategory.OperatingExpenses, 200),
            Row(OpsynexCategory.EarningsBeforeTax, 120),
            Row(OpsynexCategory.FinancialExpenses, 30),
            Row(OpsynexCategory.Depreciation, 20),
            Row(OpsynexCategory.Depreciation, 10, MappingRole.Breakdown),
            Row(OpsynexCategory.TotalLiabilitiesAndEquity, 5_000, kind: RowKind.Total),
            Row(OpsynexCategory.Equity, 2_000, kind: RowKind.Subtotal));

        year[OpsynexCategory.Revenue].ShouldBe(950);
        year.TotalCost.ShouldBe(800);
        year.Ebitda.ShouldBe(180); // 120 + 30 + (20 + 10)
        year[OpsynexCategory.TotalLiabilities].ShouldBe(3_000);
    }

    [Fact]
    public void Derived_EbtFromNetIncomeAndTax_AndMissingDataStaysNull()
    {
        var year = Consolidate(Row(OpsynexCategory.NetIncome, 70), Row(OpsynexCategory.IncomeTax, 30));

        year[OpsynexCategory.EarningsBeforeTax].ShouldBe(100);
        year.Ebitda.ShouldBe(100);
        year[OpsynexCategory.Revenue].ShouldBeNull();
        year.TotalCost.ShouldBeNull();
    }

    [Fact]
    public void Other_IsIgnored_AndYearsAreSeparated()
    {
        var years = ConsolidationEngine.Consolidate(
        [
            new ConsolidationRow(2023, OpsynexCategory.Cogs, MappingRole.Primary, RowKind.Detail, 10),
            new ConsolidationRow(2024, OpsynexCategory.Cogs, MappingRole.Primary, RowKind.Detail, 20),
            new ConsolidationRow(2024, OpsynexCategory.Other, MappingRole.Primary, RowKind.Detail, 99),
        ]);

        years.Select(y => y.Year).ShouldBe([2023, 2024]);
        years[1].Values.Keys.ShouldBe([OpsynexCategory.Cogs]);
    }

    [Fact]
    public void Interest_AndTaxes_AreUsedWhenNewCategoriesAreMissing()
    {
        var year = Consolidate(Row(OpsynexCategory.Interest, 15), Row(OpsynexCategory.Taxes, 8));

        year[OpsynexCategory.FinancialExpenses].ShouldBe(15);
        year[OpsynexCategory.IncomeTax].ShouldBe(8);
    }
}
