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
using Opsynex.Application.Homologation;
using Opsynex.Application.Uploads;
using Opsynex.Infrastructure.AiService;
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

        AddAiService(services, configuration);
        return services;
    }

    /// <summary>
    /// Cliente del servicio de IA con resiliencia: timeout total (AiService:TimeoutSeconds, 10 s),
    /// reintentos con espera exponencial y circuit breaker.
    /// </summary>
    private static void AddAiService(IServiceCollection services, IConfiguration configuration)
    {
        var timing = configuration.GetSection(AiServiceOptions.SectionName).Get<AiServiceOptions>() ?? new AiServiceOptions();
        services.AddOptions<AiServiceOptions>()
            .BindConfiguration(AiServiceOptions.SectionName)
            .Validate(o => !o.Enabled || Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out _), "AiService:BaseUrl no es una URL válida.")
            .Validate(o => o.TimeoutSeconds is >= 1 and <= 120 && o.MaxRetries is >= 0 and <= 5, "AiService: TimeoutSeconds o MaxRetries fuera de rango.");

        static void Configure(IServiceProvider sp, HttpClient http)
        {
            var options = sp.GetRequiredService<IOptions<AiServiceOptions>>().Value;
            if (Uri.TryCreate(options.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri))
            {
                http.BaseAddress = baseUri;
            }

            if (!string.IsNullOrEmpty(options.ApiKey))
            {
                http.DefaultRequestHeaders.Add(AiServiceClient.ApiKeyHeader, options.ApiKey);
            }
        }

        services.AddHttpClient<IAiServiceClient, AiServiceClient>(Configure)
            .AddStandardResilienceHandler(resilience =>
            {
                var total = TimeSpan.FromSeconds(Math.Clamp(timing.TimeoutSeconds, 1, 120));
                var attempt = TimeSpan.FromSeconds(Math.Max(1, total.TotalSeconds / (Math.Clamp(timing.MaxRetries, 0, 5) + 1.0)));
                resilience.TotalRequestTimeout.Timeout = total;
                resilience.AttemptTimeout.Timeout = attempt;
                resilience.Retry.MaxRetryAttempts = Math.Max(1, timing.MaxRetries);
                resilience.Retry.Delay = TimeSpan.FromMilliseconds(200);
                resilience.CircuitBreaker.SamplingDuration = attempt * 2 + TimeSpan.FromSeconds(1);
            });

        services.AddHttpClient(AiServiceHealthCheck.HttpClientName, (sp, http) =>
        {
            Configure(sp, http);
            http.Timeout = TimeSpan.FromSeconds(3);
        });
    }

    internal static void ConfigureSqlServer(SqlServerDbContextOptionsBuilder sql)
    {
        sql.MigrationsAssembly(typeof(OpsynexDbContext).Assembly.GetName().Name);
        sql.UseCompatibilityLevel(SqlServerCompatibilityLevel);
    }
}
