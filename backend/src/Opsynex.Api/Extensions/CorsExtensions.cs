namespace Opsynex.Api.Extensions;

public static class CorsExtensions
{
    public const string PolicyName = "Frontend";

    /// <summary>
    /// Configura CORS a partir de <c>Cors:Origins</c>. Acepta un arreglo en appsettings o una
    /// lista separada por comas en la variable de entorno <c>Cors__Origins</c>.
    /// </summary>
    public static IServiceCollection AddFrontendCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = GetOrigins(configuration);

        services.AddCors(options => options.AddPolicy(PolicyName, policy =>
        {
            if (origins.Length > 0)
            {
                policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()
                    // El frontend lee el nombre del PDF y el Retry-After del bloqueo.
                    .WithExposedHeaders("Content-Disposition", "Retry-After");
            }
        }));

        return services;
    }

    public static string[] GetOrigins(IConfiguration configuration)
    {
        var section = configuration.GetSection("Cors:Origins");
        var values = section.GetChildren().Any()
            ? section.GetChildren().Select(c => c.Value)
            : (section.Value ?? string.Empty).Split(',');

        return values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
