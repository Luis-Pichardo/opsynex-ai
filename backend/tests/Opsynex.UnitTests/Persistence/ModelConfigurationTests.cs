using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Opsynex.Domain.Common;
using Opsynex.Domain.Entities;
using Opsynex.Infrastructure;
using Opsynex.Infrastructure.Persistence;

namespace Opsynex.UnitTests.Persistence;

/// <summary>
/// Verifica la configuración del modelo de EF Core para SQL Server sin abrir conexión:
/// restricciones únicas, precisión decimal, enums como texto, columnas JSON y filtros.
/// </summary>
public sealed class ModelConfigurationTests : IDisposable
{
    private readonly OpsynexDbContext _context = new(
        new DbContextOptionsBuilder<OpsynexDbContext>()
            .UseSqlServer("Server=unit-test;Database=Opsynex", DependencyInjection.ConfigureSqlServer)
            .Options);

    private IModel Model => _context.GetService<IDesignTimeModel>().Model;

    private IEntityType Entity<T>() => Model.FindEntityType(typeof(T)) ?? throw new InvalidOperationException(typeof(T).Name);

    public void Dispose() => _context.Dispose();

    private static bool HasUniqueIndex(IEntityType entity, params string[] properties) =>
        entity.GetIndexes().Any(i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(properties));

    public static TheoryData<Type> CompanyYearEntities =>
        [typeof(FinancialData), typeof(OperationalData), typeof(KpiResult), typeof(Score)];

    [Theory]
    [MemberData(nameof(CompanyYearEntities))]
    public void CompanyYearEntities_HaveUniqueIndexOnCompanyAndYear(Type type)
    {
        var entity = Model.FindEntityType(type)!;

        HasUniqueIndex(entity, "CompanyId", "Year").ShouldBeTrue();
    }

    [Fact]
    public void User_EmailIsUnique()
    {
        Entity<User>().GetIndexes().ShouldContain(i => i.IsUnique && i.Properties.Single().Name == nameof(User.Email));
    }

    [Fact]
    public void AccountMapping_IsUniquePerCompanyScopeSectionAndClientAccount()
    {
        HasUniqueIndex(Entity<AccountMapping>(), "CompanyId", "Scope", "Section", "ClientAccount").ShouldBeTrue();
    }

    [Fact]
    public void Problem_IsUniquePerCompanyYearAndCode()
    {
        HasUniqueIndex(Entity<Problem>(), "CompanyId", "Year", "Code").ShouldBeTrue();
    }

    [Theory]
    [InlineData(typeof(FinancialData), nameof(FinancialData.Revenue))]
    [InlineData(typeof(FinancialData), nameof(FinancialData.OperatingCashFlow))]
    [InlineData(typeof(OperationalData), nameof(OperationalData.EnergyCost))]
    [InlineData(typeof(RawAccountValue), nameof(RawAccountValue.Value))]
    [InlineData(typeof(Problem), nameof(Problem.ImpactValue))]
    [InlineData(typeof(KpiResult), nameof(KpiResult.RevenuePerEmployee))]
    public void Amounts_AreDecimal18_2(Type type, string property)
    {
        Model.FindEntityType(type)!.FindProperty(property)!.GetColumnType().ShouldBe("decimal(18,2)");
    }

    [Theory]
    [InlineData(nameof(KpiResult.EbitdaMargin))]
    [InlineData(nameof(KpiResult.GrossMargin))]
    [InlineData(nameof(KpiResult.CostEfficiency))]
    [InlineData(nameof(KpiResult.OperatingCostRatio))]
    public void Ratios_AreDecimal9_4(string property)
    {
        Entity<KpiResult>().FindProperty(property)!.GetColumnType().ShouldBe("decimal(9,4)");
    }

    [Fact]
    public void Scores_AreDecimal5_2_AndConfidenceIs5_4()
    {
        Entity<Score>().FindProperty(nameof(Score.TotalScore))!.GetColumnType().ShouldBe("decimal(5,2)");
        Entity<AccountMapping>().FindProperty(nameof(AccountMapping.Confidence))!.GetColumnType().ShouldBe("decimal(5,4)");
    }

    [Fact]
    public void AllDecimalProperties_HaveExplicitPrecision()
    {
        var decimals = Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => Nullable.GetUnderlyingType(p.ClrType) == typeof(decimal) || p.ClrType == typeof(decimal));

        decimals.ShouldAllBe(p => p.GetPrecision() != null && p.GetScale() != null);
    }

    [Fact]
    public void Enums_AreStoredAsStrings()
    {
        var enums = Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => (Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType).IsEnum)
            .ToList();

        enums.ShouldNotBeEmpty();
        enums.ShouldAllBe(p => p.GetProviderClrType() == typeof(string) && p.GetMaxLength() == 50);
    }

    [Theory]
    [InlineData(typeof(Upload), nameof(Upload.Errors))]
    [InlineData(typeof(Problem), nameof(Problem.Evidence))]
    [InlineData(typeof(Score), nameof(Score.WeightsUsed))]
    public void JsonColumns_AreNvarcharMaxWithIsJsonCheck(Type type, string property)
    {
        var entity = Model.FindEntityType(type)!;

        entity.FindProperty(property)!.GetColumnType().ShouldBe("nvarchar(max)");
        entity.GetCheckConstraints().ShouldContain(c => c.Sql == $"ISJSON([{property}]) = 1");
    }

    [Fact]
    public void AllEntities_HaveCompanyQueryFilter()
    {
        foreach (var entity in Model.GetEntityTypes())
        {
            entity.FindDeclaredQueryFilter(OpsynexDbContext.CompanyFilter).ShouldNotBeNull(entity.DisplayName());
        }
    }

    [Fact]
    public void CompanyScopedEntities_HaveIndexStartingWithCompanyId()
    {
        var scoped = Model.GetEntityTypes().Where(e => typeof(ICompanyScoped).IsAssignableFrom(e.ClrType));

        foreach (var entity in scoped)
        {
            entity.GetIndexes().ShouldContain(i => i.Properties[0].Name == "CompanyId", entity.DisplayName());
        }
    }

    [Fact]
    public void ForeignKeysToCompany_DoNotCascade()
    {
        var fks = Model.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys())
            .Where(fk => fk.PrincipalEntityType.ClrType == typeof(Company));

        fks.ShouldNotBeEmpty();
        fks.ShouldAllBe(fk => fk.DeleteBehavior == DeleteBehavior.Restrict);
    }

    [Fact]
    public void DeletingUpload_CascadesToRawValues()
    {
        var fk = Entity<RawAccountValue>().GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(Upload));

        fk.DeleteBehavior.ShouldBe(DeleteBehavior.Cascade);
    }

    [Fact]
    public void Model_HasNoPendingChangesComparedToLastMigration()
    {
        _context.Database.HasPendingModelChanges().ShouldBeFalse(
            "Hay cambios en el modelo sin migración: ejecuta 'dotnet ef migrations add <Nombre>'.");
    }
}
