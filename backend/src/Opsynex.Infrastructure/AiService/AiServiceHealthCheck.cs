using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Opsynex.Infrastructure.AiService;

/// <summary>
/// Salud del servicio de IA. Si no responde el estado es <c>Degraded</c> (no <c>Unhealthy</c>):
/// la plataforma sigue funcionando con el fallback local.
/// </summary>
public sealed class AiServiceHealthCheck(IHttpClientFactory httpClientFactory, IOptions<AiServiceOptions> options) : IHealthCheck
{
    public const string HttpClientName = "ai-service-health";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            return HealthCheckResult.Degraded("El servicio de IA está desactivado; se usa el fallback local.");
        }

        try
        {
            using var http = httpClientFactory.CreateClient(HttpClientName);
            using var response = await http.GetAsync(new Uri("health", UriKind.Relative), cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("El servicio de IA responde.")
                : HealthCheckResult.Degraded($"El servicio de IA respondió {(int)response.StatusCode}; se usa el fallback local.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return HealthCheckResult.Degraded("El servicio de IA no responde; se usa el fallback local.");
        }
    }
}
