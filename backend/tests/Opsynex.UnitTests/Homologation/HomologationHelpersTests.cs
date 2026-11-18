using Opsynex.Application.Homologation;
using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;

namespace Opsynex.UnitTests.Homologation;

public sealed class HomologationHelpersTests
{
    private static RawAccountValue Raw(SheetType type, string label, RowKind kind = RowKind.Detail, string? section = null) => new()
    {
        ClientAccount = label,
        NormalizedLabel = label,
        SheetType = type,
        RowKind = kind,
        Section = section,
    };

    [Fact]
    public void AccountKey_SeparatesStatementSubtotalsBySection()
    {
        var assets = AccountKey.For(Raw(SheetType.BalanceSheet, "Total corriente", RowKind.Subtotal, "ACTIVOS CORRIENTES"))!;
        var liabilities = AccountKey.For(Raw(SheetType.BalanceSheet, "Total corriente", RowKind.Subtotal, "PASIVO CORRIENTE"))!;

        assets.Id.ShouldNotBe(liabilities.Id);
        assets.Section.ShouldBe("activos corrientes");
    }

    [Fact]
    public void AccountKey_IgnoresSectionForDetailsAndNotes()
    {
        var a = AccountKey.For(Raw(SheetType.Notes, "Energía Eléctrica", section: "13. GASTOS"))!;
        var b = AccountKey.For(Raw(SheetType.Notes, "ENERGIA ELECTRICA", section: "12. COSTO DE PRODUCCION"))!;

        a.Id.ShouldBe(b.Id);
        a.Scope.ShouldBe(MappingScope.Notes);
        AccountKey.RoleFor(a.Scope).ShouldBe(MappingRole.Breakdown);
    }

    [Theory]
    [InlineData(SheetType.TrialBalance)]
    [InlineData(SheetType.Operational)]
    [InlineData(SheetType.EmployeeRoster)]
    public void AccountKey_ExcludesSheetsThatAreNotHomologated(SheetType type) =>
        AccountKey.For(Raw(type, "Bancos")).ShouldBeNull();

    [Fact]
    public void CategoryNames_RoundTrip()
    {
        foreach (var category in Enum.GetValues<OpsynexCategory>())
        {
            CategoryNames.Parse(CategoryNames.ToSnakeCase(category)).ShouldBe(category);
        }

        CategoryNames.ToSnakeCase(OpsynexCategory.TotalLiabilitiesAndEquity).ShouldBe("total_liabilities_and_equity");
        CategoryNames.Parse("no_existe").ShouldBeNull();
    }

    [Theory]
    [InlineData(MappingScope.Statement, "", "Ventas netas", OpsynexCategory.GrossSales)]
    [InlineData(MappingScope.Statement, "activos corrientes", "Total corriente", OpsynexCategory.CurrentAssets)]
    [InlineData(MappingScope.Statement, "pasivo corriente", "Total corriente", OpsynexCategory.CurrentLiabilities)]
    [InlineData(MappingScope.Notes, "", "Energía Eléctrica", OpsynexCategory.EnergyCost)]
    [InlineData(MappingScope.Statement, "", "Total Pasivos & Capital", OpsynexCategory.TotalLiabilitiesAndEquity)]
    public void Fallback_KnowsCommonLabels(MappingScope scope, string section, string label, OpsynexCategory expected) =>
        FallbackSuggester.Find(new AccountKey(scope, section, label), label).ShouldBe(expected);

    [Fact]
    public void Fallback_ReturnsNullForUnknownLabels() =>
        FallbackSuggester.Find(new AccountKey(MappingScope.Statement, string.Empty, "Partida especial ZQX"), "Partida especial ZQX").ShouldBeNull();
}
