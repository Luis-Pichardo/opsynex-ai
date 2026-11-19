using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Opsynex.Application;

/// <summary>Registro de servicios de la capa de aplicación.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Registra todos los validadores de FluentValidation de este ensamblado.
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddOptions<Auth.AuthOptions>()
            .BindConfiguration(Auth.AuthOptions.SectionName)
            .Validate(o => o.MaxFailedAttempts > 0 && o.LockoutMinutes > 0, "Auth: MaxFailedAttempts y LockoutMinutes deben ser mayores que 0.")
            .ValidateOnStart();

        services.AddScoped<Auth.AuthService>();
        services.AddScoped<Users.UserService>();
        services.AddScoped<Companies.CompanyService>();
        services.AddScoped<Uploads.UploadService>();
        services.AddScoped<Uploads.DataService>();
        services.AddScoped<Homologation.HomologationService>();
        services.AddScoped<Analysis.CompanyScope>();
        services.AddScoped<Analysis.AnalysisPipeline>();
        services.AddScoped<Analysis.KpiService>();
        services.AddScoped<Analysis.ProblemService>();
        services.AddScoped<Analysis.ScoreService>();
        services.AddScoped<Analysis.AnalysisSummaryService>();
        services.AddScoped<Reports.ReportService>();
        services.AddScoped<Analysis.ImpactService>();

        // Reglas de diagnóstico: todas las implementaciones de IDiagnosticRule del dominio.
        foreach (var rule in typeof(Domain.Engines.Diagnosis.IDiagnosticRule).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(Domain.Engines.Diagnosis.IDiagnosticRule).IsAssignableFrom(t)))
        {
            services.AddSingleton(typeof(Domain.Engines.Diagnosis.IDiagnosticRule), rule);
        }

        services.AddSingleton<Domain.Engines.Diagnosis.DiagnosisEngine>();
        services.AddOptions<Domain.Engines.Diagnosis.DiagnosisSettings>()
            .BindConfiguration(Domain.Engines.Diagnosis.DiagnosisSettings.SectionName)
            .Validate(
                o => o.Rules.Values.All(r => r.Low >= 0 && r.Low <= r.Medium && r.Medium <= r.High),
                "DiagnosisRules: cada regla necesita 0 <= Low <= Medium <= High.")
            .ValidateOnStart();
        services.AddOptions<Domain.Engines.Scoring.ScoreSettings>()
            .BindConfiguration(Domain.Engines.Scoring.ScoreSettings.SectionName)
            .Validate(o => !o.Validate().Any(), "Score: pesos o parámetros inválidos (los pesos deben sumar 1).")
            .ValidateOnStart();
        services.AddOptions<Analysis.BenchmarkOptions>()
            .BindConfiguration(Analysis.BenchmarkOptions.SectionName)
            .Validate(o => !o.InvalidKeys().Any(), "Benchmarks: hay KPIs desconocidos en la configuración.")
            .Validate(o => o.StableThresholdPercent is >= 0 and <= 50, "Benchmarks:StableThresholdPercent fuera de rango.")
            .ValidateOnStart();
        services.AddScoped<Homologation.UploadProcessingService>();
        services.AddOptions<Uploads.UploadOptions>()
            .BindConfiguration(Uploads.UploadOptions.SectionName)
            .Validate(o => o.MaxMb is > 0 and <= 200 && o.MaxRowsPerSheet > 0 && o.MaxSheets > 0, "Upload: valores fuera de rango.")
            .ValidateOnStart();
        return services;
    }
}
