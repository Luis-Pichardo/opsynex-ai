using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Opsynex.Application.Common.Exceptions;
using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;
using Opsynex.Infrastructure.Persistence;
using Opsynex.Infrastructure.Persistence.Repositories;
using Opsynex.Infrastructure.Tenancy;
using Opsynex.IntegrationTests.Infrastructure;

namespace Opsynex.IntegrationTests.Persistence;

/// <summary>Repositorios contra la BD real (SQL Server en Docker o SQLite como respaldo).</summary>
public sealed class RepositoryTests(DatabaseFixture db)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CompanyYearRepository_CreatesAndReadsByCompanyAndYear()
    {
        await using var context = db.CreateContext();
        var companyId = await TestData.SeedCompanyAsync(context, Ct);
        var repository = new CompanyYearRepository<FinancialData>(context);
        var unitOfWork = new UnitOfWork(context);

        await repository.AddRangeAsync(
            [TestData.NewFinancialData(companyId, 2024, 1_200_000m), TestData.NewFinancialData(companyId, 2022), TestData.NewFinancialData(companyId, 2023)],
            Ct);
        await unitOfWork.SaveChangesAsync(Ct);

        await using var readContext = db.CreateContext();
        var readRepository = new CompanyYearRepository<FinancialData>(readContext);
        var data2024 = await readRepository.GetByCompanyAndYearAsync(companyId, 2024, Ct);
        var all = await readRepository.ListByCompanyAsync(companyId, Ct);

        data2024.ShouldNotBeNull();
        data2024.Revenue.ShouldBe(1_200_000m);
        data2024.Cogs.ShouldBe(720_000m);
        data2024.TotalAssets.ShouldBeNull();
        all.Select(f => f.Year).ShouldBe([2022, 2023, 2024]);
    }

    [Fact]
    public async Task SaveChanges_SetsAuditTimestampsInUtc()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero));
        await using var context = db.CreateContext(timeProvider: clock);
        var company = TestData.NewCompany();
        context.Companies.Add(company);
        await context.SaveChangesAsync(Ct);

        clock.Advance(TimeSpan.FromHours(2));
        company.Sector = "Metalmecánica";
        await context.SaveChangesAsync(Ct);

        await using var readContext = db.CreateContext();
        var saved = await readContext.Companies.SingleAsync(c => c.Id == company.Id, Ct);
        saved.CreatedAt.ShouldBe(new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc));
        saved.UpdatedAt.ShouldBe(new DateTime(2026, 1, 10, 14, 0, 0, DateTimeKind.Utc));
        saved.CreatedAt.Kind.ShouldBe(DateTimeKind.Utc);
        saved.Currency.ShouldBe("USD");
    }

    [Fact]
    public async Task CompanyFilter_LimitsQueriesToTenantCompany()
    {
        Guid companyA, companyB;
        await using (var setup = db.CreateContext())
        {
            companyA = await TestData.SeedCompanyAsync(setup, Ct);
            companyB = await TestData.SeedCompanyAsync(setup, Ct);
            setup.FinancialData.AddRange(TestData.NewFinancialData(companyA, 2024), TestData.NewFinancialData(companyB, 2024));
            setup.Users.AddRange(TestData.NewUser(companyA), TestData.NewUser(companyB));
            await setup.SaveChangesAsync(Ct);
        }

        await using var tenantContext = db.CreateContext(new FixedTenant(companyA));
        var financial = await new CompanyYearRepository<FinancialData>(tenantContext)
            .ListAsync(f => f.CompanyId == companyA || f.CompanyId == companyB, Ct);
        var companies = await new Repository<Company>(tenantContext)
            .ListAsync(c => c.Id == companyA || c.Id == companyB, Ct);
        var users = await new Repository<User>(tenantContext)
            .ListAsync(u => u.CompanyId == companyA || u.CompanyId == companyB, Ct);
        var otherCompanyData = await new CompanyYearRepository<FinancialData>(tenantContext)
            .GetByCompanyAndYearAsync(companyB, 2024, Ct);

        financial.ShouldHaveSingleItem().CompanyId.ShouldBe(companyA);
        companies.ShouldHaveSingleItem().Id.ShouldBe(companyA);
        users.ShouldHaveSingleItem().CompanyId.ShouldBe(companyA);
        otherCompanyData.ShouldBeNull();

        // Un admin (sin filtro) ve ambas empresas.
        await using var adminContext = db.CreateContext(new UnrestrictedTenant());
        (await adminContext.FinancialData.CountAsync(f => f.CompanyId == companyA || f.CompanyId == companyB, Ct)).ShouldBe(2);
    }

    [Fact]
    public async Task FinancialData_DuplicateCompanyAndYear_ThrowsUniqueViolation()
    {
        await using var context = db.CreateContext();
        var companyId = await TestData.SeedCompanyAsync(context, Ct);
        var otherCompanyId = await TestData.SeedCompanyAsync(context, Ct);
        context.FinancialData.Add(TestData.NewFinancialData(companyId, 2024));
        context.FinancialData.Add(TestData.NewFinancialData(otherCompanyId, 2024)); // otra empresa: permitido
        await new UnitOfWork(context).SaveChangesAsync(Ct);

        await using var duplicateContext = db.CreateContext();
        duplicateContext.FinancialData.Add(TestData.NewFinancialData(companyId, 2024));

        await Should.ThrowAsync<UniqueConstraintViolationException>(() => new UnitOfWork(duplicateContext).SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task AccountMapping_DuplicateClientAccount_ThrowsUniqueViolation()
    {
        await using var context = db.CreateContext();
        var companyId = await TestData.SeedCompanyAsync(context, Ct);
        var repository = new AccountMappingRepository(context);
        await repository.AddAsync(NewMapping(companyId, "Ventas nacionales", OpsynexCategory.Revenue), Ct);
        await repository.AddAsync(NewMapping(companyId, "Costo de mercadería", OpsynexCategory.Cogs), Ct);
        await new UnitOfWork(context).SaveChangesAsync(Ct);

        (await repository.GetByClientAccountAsync(companyId, "Ventas nacionales", Ct))!.OpsynexCategory.ShouldBe(OpsynexCategory.Revenue);
        (await repository.ListByCompanyAsync(companyId, Ct)).Select(m => m.ClientAccount)
            .ShouldBe(["Costo de mercadería", "Ventas nacionales"]);

        await using var duplicateContext = db.CreateContext();
        duplicateContext.AccountMappings.Add(NewMapping(companyId, "Ventas nacionales", OpsynexCategory.Other));
        await Should.ThrowAsync<UniqueConstraintViolationException>(() => new UnitOfWork(duplicateContext).SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task User_EmailIsNormalizedAndUnique()
    {
        var local = $"Ana.{Guid.NewGuid():N}";
        await using var context = db.CreateContext();
        var companyId = await TestData.SeedCompanyAsync(context, Ct);
        context.Users.Add(TestData.NewUser(companyId, $"  {local}@Opsynex.TEST "));
        await new UnitOfWork(context).SaveChangesAsync(Ct);

        // El login busca sin importar mayúsculas y aunque haya un filtro por otra empresa.
        await using var otherTenant = db.CreateContext(new FixedTenant(Guid.NewGuid()));
        var found = await new UserRepository(otherTenant).GetByEmailAsync($"{local.ToUpperInvariant()}@opsynex.test", Ct);
        found.ShouldNotBeNull();
        found.Email.ShouldBe($"{local}@opsynex.test".ToLowerInvariant());

        await using var duplicateContext = db.CreateContext();
        duplicateContext.Users.Add(TestData.NewUser(companyId, $"{local}@opsynex.test"));
        await Should.ThrowAsync<UniqueConstraintViolationException>(() => new UnitOfWork(duplicateContext).SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task GlobalAdmin_CanExistWithoutCompany()
    {
        await using var context = db.CreateContext();
        var admin = TestData.NewUser(null, role: UserRole.Admin);
        context.Users.Add(admin);
        await context.SaveChangesAsync(Ct);

        await using var readContext = db.CreateContext();
        var saved = await new UserRepository(readContext).GetByIdAsync(admin.Id, Ct);
        saved.ShouldNotBeNull();
        saved.CompanyId.ShouldBeNull();
        saved.Role.ShouldBe(UserRole.Admin);
        saved.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task JsonColumns_RoundTripAndTrackChanges()
    {
        await using var context = db.CreateContext();
        var companyId = await TestData.SeedCompanyAsync(context, Ct);
        var user = TestData.NewUser(companyId);
        context.Users.Add(user);
        var upload = new Upload
        {
            CompanyId = companyId,
            UserId = user.Id,
            FileName = "estado_resultados.xlsx",
            FileType = UploadFileType.IncomeStatement,
            Errors = [new UploadError("missing_column", "Falta la columna «2024».", Row: 1, Column: "C")],
        };
        var problem = new Problem
        {
            CompanyId = companyId,
            Year = 2024,
            Code = "EBITDA_DECLINE",
            Title = "Deterioro de rentabilidad",
            Description = "El margen EBITDA bajó 4,2 pp.",
            Recommendation = "Revisar la estructura de costos.",
            Category = ProblemCategory.Profitability,
            Severity = Severity.Medium,
            ImpactCategory = ImpactCategory.MarginLoss,
            ImpactValue = 52_500.75m,
            Evidence = new JsonObject { ["previous"] = 0.18m, ["current"] = 0.138m, ["kpi"] = "EbitdaMargin" },
        };
        var score = new Score
        {
            CompanyId = companyId,
            Year = 2024,
            TotalScore = 72.5m,
            ProfitabilityScore = 65m,
            CostEfficiencyScore = 70m,
            ProductivityScore = 80m,
            FinancialStabilityScore = 75m,
            WeightsUsed = new Dictionary<string, decimal> { ["Profitability"] = 0.35m, ["CostEfficiency"] = 0.25m },
            Interpretation = ScoreInterpretation.Improvable,
        };
        context.AddRange(upload, problem, score);
        await context.SaveChangesAsync(Ct);

        // Cambios dentro del JSON deben detectarse (ValueComparer).
        upload.Errors.Add(new UploadError("invalid_number", "Valor no numérico.", 4, "D"));
        problem.Evidence["impactFormula"] = "revenue * deltaMargin";
        score.WeightsUsed["Productivity"] = 0.2m;
        await context.SaveChangesAsync(Ct);

        await using var readContext = db.CreateContext();
        var savedUpload = await readContext.Uploads.SingleAsync(u => u.Id == upload.Id, Ct);
        var savedProblem = await readContext.Problems.SingleAsync(p => p.Id == problem.Id, Ct);
        var savedScore = await readContext.Scores.SingleAsync(s => s.Id == score.Id, Ct);

        savedUpload.Status.ShouldBe(UploadStatus.Pending);
        savedUpload.Errors.Count.ShouldBe(2);
        savedUpload.Errors[0].ShouldBe(new UploadError("missing_column", "Falta la columna «2024».", 1, "C"));
        savedProblem.Evidence["kpi"]!.GetValue<string>().ShouldBe("EbitdaMargin");
        savedProblem.Evidence["impactFormula"]!.GetValue<string>().ShouldBe("revenue * deltaMargin");
        savedProblem.Status.ShouldBe(ProblemStatus.Open);
        savedProblem.ImpactValue.ShouldBe(52_500.75m);
        savedScore.WeightsUsed.ShouldContainKeyAndValue("Productivity", 0.2m);
        savedScore.Interpretation.ShouldBe(ScoreInterpretation.Improvable);
    }

    [Fact]
    public async Task DeletingUpload_CascadesToRawValues()
    {
        await using var context = db.CreateContext();
        var companyId = await TestData.SeedCompanyAsync(context, Ct);
        var user = TestData.NewUser(companyId);
        var upload = new Upload { CompanyId = companyId, UserId = user.Id, FileName = "balance.csv", FileType = UploadFileType.BalanceSheet };
        upload.RawValues.Add(new RawAccountValue { CompanyId = companyId, ClientAccount = "Caja", Year = 2024, Value = 10.5m });
        upload.RawValues.Add(new RawAccountValue { CompanyId = companyId, ClientAccount = "Bancos", Year = 2024, Value = -3m });
        context.AddRange(user, upload);
        await context.SaveChangesAsync(Ct);

        context.Uploads.Remove(upload);
        await context.SaveChangesAsync(Ct);

        await using var readContext = db.CreateContext();
        (await readContext.RawAccountValues.CountAsync(r => r.UploadId == upload.Id, Ct)).ShouldBe(0);
    }

    [Fact]
    public async Task KpiRatios_AreRoundedToFourDecimals()
    {
        Assert.SkipUnless(db.IsSqlServer, "SQLite no aplica la precisión decimal.");

        await using var context = db.CreateContext();
        var companyId = await TestData.SeedCompanyAsync(context, Ct);
        var kpi = new KpiResult { CompanyId = companyId, Year = 2024, EbitdaMargin = 0.123456m, RevenuePerEmployee = 123_456.789m, CalculatedAt = DateTime.UtcNow };
        context.KpiResults.Add(kpi);
        await context.SaveChangesAsync(Ct);

        await using var readContext = db.CreateContext();
        var saved = await readContext.KpiResults.SingleAsync(k => k.Id == kpi.Id, Ct);
        saved.EbitdaMargin.ShouldBe(0.1235m);
        saved.RevenuePerEmployee.ShouldBe(123_456.79m);
        saved.GrossMargin.ShouldBeNull();
    }

    [Fact]
    public async Task YearOutOfRange_IsRejectedByCheckConstraint()
    {
        await using var context = db.CreateContext();
        var companyId = await TestData.SeedCompanyAsync(context, Ct);
        context.FinancialData.Add(TestData.NewFinancialData(companyId, 24));

        await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));
    }

    private static AccountMapping NewMapping(Guid companyId, string account, OpsynexCategory category) => new()
    {
        CompanyId = companyId,
        ClientAccount = account,
        OpsynexCategory = category,
        Confidence = 0.9123m,
        Method = MappingMethod.Fuzzy,
    };
}
