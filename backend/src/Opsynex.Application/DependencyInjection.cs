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
        return services;
    }
}
