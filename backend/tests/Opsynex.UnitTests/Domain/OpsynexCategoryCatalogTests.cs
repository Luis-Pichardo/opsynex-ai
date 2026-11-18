using Opsynex.Domain.Catalogs;
using Opsynex.Domain.Enums;

namespace Opsynex.UnitTests.Domain;

public sealed class OpsynexCategoryCatalogTests
{
    [Fact]
    public void Catalog_HasOneEntryPerCategoryInEnumOrder()
    {
        OpsynexCategoryCatalog.All.Select(i => i.Category).ShouldBe(Enum.GetValues<OpsynexCategory>());
    }

    [Fact]
    public void Catalog_EntriesHaveSpanishNameAndDescription()
    {
        OpsynexCategoryCatalog.All.ShouldAllBe(i => !string.IsNullOrWhiteSpace(i.Name) && !string.IsNullOrWhiteSpace(i.Description));
    }

    [Fact]
    public void Get_ReturnsCategoryInfo()
    {
        var info = OpsynexCategoryCatalog.Get(OpsynexCategory.Revenue);

        info.Name.ShouldBe("Ingresos");
        info.Statement.ShouldBe(FinancialStatement.IncomeStatement);
    }
}
