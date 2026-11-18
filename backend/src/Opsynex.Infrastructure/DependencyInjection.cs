using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Opsynex.Application.Abstractions.Persistence;
using Opsynex.Application.Abstractions.Security;
using Opsynex.Infrastructure.Security;
using Opsynex.Application.Abstractions.Tenancy;
using Opsynex.Domain.Entities;
using Opsynex.Application.Uploads;
using Opsynex.Infrastructure.Parsing;
using Opsynex.Infrastructure.Persistence;
using Opsynex.Infrastructure.Persistence.Interceptors;
using Opsynex.Infrastructure.Persistence.Repositories;
using Opsynex.Infrastructure.Tenancy;

namespace Opsynex.Infrastructure;

/// <summary>Registro de servicios de infraestructura (EF Core, parsers, cliente de IA).</summary>
public static class DependencyInjection
{
    /// <summary>SQL Server 2022 = nivel de compatibilidad 160.</summary>
    public const int SqlServerCompatibilityLevel = 160;

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<ICurrentTenant, UnrestrictedTenant>();
        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<OpsynexDbContext>((sp, options) =>
        {
            var connectionString = configuration.GetConnectionString("Default");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Falta la cadena de conexión 'ConnectionStrings:Default' (variable ConnectionStrings__Default).");
            }

            var databaseOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            if (databaseOptions.Provider == DatabaseProvider.Sqlite)
            {
                options.UseSqlite(connectionString);
            }
            else
            {
                options.UseSqlServer(connectionString, ConfigureSqlServer);
            }

            options.AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped(typeof(ICompanyYearRepository<>), typeof(CompanyYearRepository<>));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<IWorkbookParser, WorkbookParser>();
        services.AddSingleton<IUploadTemplateGenerator, TemplateGenerator>();
        services.AddScoped<IAccountMappingRepository, AccountMappingRepository>();

        // Etapa 04: cliente HTTP del servicio de IA.
        return services;
    }

    internal static void ConfigureSqlServer(SqlServerDbContextOptionsBuilder sql)
    {
        sql.MigrationsAssembly(typeof(OpsynexDbContext).Assembly.GetName().Name);
        sql.UseCompatibilityLevel(SqlServerCompatibilityLevel);
    }
}
