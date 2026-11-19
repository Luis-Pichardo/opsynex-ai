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
        services.AddOptions<Uploads.UploadOptions>()
            .BindConfiguration(Uploads.UploadOptions.SectionName)
            .Validate(o => o.MaxMb is > 0 and <= 200 && o.MaxRowsPerSheet > 0 && o.MaxSheets > 0, "Upload: valores fuera de rango.")
            .ValidateOnStart();
        return services;
    }
}
