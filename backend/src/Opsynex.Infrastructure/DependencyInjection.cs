using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Opsynex.Infrastructure;

/// <summary>Registro de servicios de infraestructura (EF Core, parsers, cliente de IA).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        // Etapa 01: DbContext de EF Core con SQL Server.
        // Etapa 04: cliente HTTP del servicio de IA.
        return services;
    }
}
