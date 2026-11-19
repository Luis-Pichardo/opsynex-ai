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
        return services;
    }
}
