using Microsoft.EntityFrameworkCore;
using Opsynex.Application.Abstractions.Tenancy;
using Opsynex.Domain.Common;
using Opsynex.Domain.Entities;
using Opsynex.Infrastructure.Persistence.Converters;
using Opsynex.Infrastructure.Tenancy;

namespace Opsynex.Infrastructure.Persistence;

/// <summary>Contexto de EF Core de Opsynex AI.</summary>
public sealed class OpsynexDbContext(DbContextOptions<OpsynexDbContext> options, ICurrentTenant? tenant = null)
    : DbContext(options)
{
    /// <summary>Nombre del filtro global por empresa (se puede ignorar con IgnoreQueryFilters).</summary>
    public const string CompanyFilter = "Company";

    private readonly ICurrentTenant _tenant = tenant ?? new UnrestrictedTenant();

    // Propiedades leídas por los filtros globales; EF las evalúa en cada consulta.
    internal bool CompanyFilterEnabled => _tenant.IsFilterEnabled;

    // Con el filtro activo y sin empresa (p. ej. petición anónima) no coincide ningún registro.
    internal Guid CurrentCompanyId => _tenant.CompanyId ?? Guid.Empty;

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Upload> Uploads => Set<Upload>();

    public DbSet<RawAccountValue> RawAccountValues => Set<RawAccountValue>();

    public DbSet<AccountMapping> AccountMappings => Set<AccountMapping>();

    public DbSet<FinancialData> FinancialData => Set<FinancialData>();

    public DbSet<OperationalData> OperationalData => Set<OperationalData>();

    public DbSet<MonthlyFinancialData> MonthlyFinancialData => Set<MonthlyFinancialData>();

    public DbSet<KpiResult> KpiResults => Set<KpiResult>();

    public DbSet<Problem> Problems => Set<Problem>();

    public DbSet<Score> Scores => Set<Score>();

    public DbSet<ExecutiveSummary> ExecutiveSummaries => Set<ExecutiveSummary>();

    public DbSet<ProductionData> ProductionData => Set<ProductionData>();

    public DbSet<CostData> CostData => Set<CostData>();

    public DbSet<StandardData> StandardData => Set<StandardData>();

    public DbSet<IndustrialKpiResult> IndustrialKpiResults => Set<IndustrialKpiResult>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Montos: decimal(18,2). Los ratios se sobrescriben a decimal(9,4) en cada configuración.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        // Enums como texto legible.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(50);
        // Fechas siempre en UTC.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OpsynexDbContext).Assembly);

        if (Database.IsSqlite())
        {
            RemoveChecksUnsupportedBySqlite(modelBuilder);
        }

        // Filtro global por empresa: los usuarios no Admin solo ven datos de su empresa.
        modelBuilder.Entity<Company>().HasQueryFilter(
            CompanyFilter, c => !CompanyFilterEnabled || c.Id == CurrentCompanyId);
        modelBuilder.Entity<User>().HasQueryFilter(
            CompanyFilter, u => !CompanyFilterEnabled || u.CompanyId == CurrentCompanyId);

        ApplyCompanyFilter<Upload>(modelBuilder);
        ApplyCompanyFilter<RawAccountValue>(modelBuilder);
        ApplyCompanyFilter<AccountMapping>(modelBuilder);
        ApplyCompanyFilter<FinancialData>(modelBuilder);
        ApplyCompanyFilter<OperationalData>(modelBuilder);
        ApplyCompanyFilter<MonthlyFinancialData>(modelBuilder);
        ApplyCompanyFilter<KpiResult>(modelBuilder);
        ApplyCompanyFilter<Problem>(modelBuilder);
        ApplyCompanyFilter<Score>(modelBuilder);
        ApplyCompanyFilter<ExecutiveSummary>(modelBuilder);
        ApplyCompanyFilter<ProductionData>(modelBuilder);
        ApplyCompanyFilter<CostData>(modelBuilder);
        ApplyCompanyFilter<StandardData>(modelBuilder);
        ApplyCompanyFilter<IndustrialKpiResult>(modelBuilder);
    }

    /// <summary>
    /// SQLite (solo pruebas) no tiene ISJSON y guarda los decimal como texto, así que solo se
    /// conservan las restricciones sobre enteros (año).
    /// </summary>
    private static void RemoveChecksUnsupportedBySqlite(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var unsupported = entityType.GetCheckConstraints()
                .Where(c => !c.ModelName.EndsWith("_Year", StringComparison.Ordinal) && !c.ModelName.EndsWith("_Month", StringComparison.Ordinal))
                .Select(c => c.ModelName)
                .ToList();
            foreach (var name in unsupported)
            {
                entityType.RemoveCheckConstraint(name);
            }
        }
    }

    private void ApplyCompanyFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : Entity, ICompanyScoped =>
        modelBuilder.Entity<TEntity>().HasQueryFilter(
            CompanyFilter, e => !CompanyFilterEnabled || e.CompanyId == CurrentCompanyId);
}
