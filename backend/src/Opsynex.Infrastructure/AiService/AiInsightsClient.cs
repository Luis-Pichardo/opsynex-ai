using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Opsynex.Application.Homologation;
using Opsynex.Application.Insights;

namespace Opsynex.Infrastructure.AiService;

/// <summary>
/// Cliente de recomendaciones, resumen ejecutivo y anomalías del servicio de IA (Python).
/// Traduce los errores: 503 <c>llm_not_configured</c> → <see cref="AiNotConfiguredException"/>;
/// cualquier otro fallo → <see cref="AiServiceUnavailableException"/> con el mensaje del servicio.
/// </summary>
internal sealed class AiInsightsClient(HttpClient http, IOptions<AiServiceOptions> options) : IAiInsightsClient
{
    public async Task<AiRecommendation> GenerateRecommendationAsync(AiRecommendationRequest request, CancellationToken cancellationToken)
    {
        var body = await PostAsync<RecommendationOut>("recommendations/generate", request, cancellationToken);
        return new AiRecommendation(
            body.Summary,
            body.RootCauses,
            [.. body.Actions.Select(a => new AiAction(a.Title, a.Description, a.ExpectedImpact, a.Effort, a.Horizon))],
            body.Model,
            default,
            string.Empty);
    }

    public async Task<AiSummaryResult> GenerateSummaryAsync(AiSummaryRequest request, CancellationToken cancellationToken)
    {
        var body = await PostAsync<SummaryOut>("executive-summary", request, cancellationToken);
        return new AiSummaryResult(body.Paragraphs, body.Priorities, body.Model);
    }

    public async Task<IReadOnlyList<AiAnomaly>> DetectAnomaliesAsync(AiAnomalyRequest request, CancellationToken cancellationToken)
    {
        var body = await PostAsync<AnomaliesOut>("anomalies/detect", request, cancellationToken);
        return body.Anomalies;
    }

    private async Task<T> PostAsync<T>(string path, object request, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            throw new AiServiceUnavailableException("El servicio de IA está desactivado (AiService:Enabled = false).");
        }

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync(path, request, AiServiceClient.Json, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new AiServiceUnavailableException($"No se pudo llamar al servicio de IA: {ex.Message}", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var problem = await ReadProblemAsync(response, cancellationToken);
                if (response.StatusCode == HttpStatusCode.ServiceUnavailable && problem?.Code == "llm_not_configured")
                {
                    throw new AiNotConfiguredException(problem.Detail ?? "La IA generativa no está configurada.");
                }

                throw new AiServiceUnavailableException(problem?.Detail ?? $"El servicio de IA respondió {(int)response.StatusCode}.");
            }

            try
            {
                return await response.Content.ReadFromJsonAsync<T>(AiServiceClient.Json, cancellationToken)
                    ?? throw new AiServiceUnavailableException("El servicio de IA respondió vacío.");
            }
            catch (JsonException ex)
            {
                throw new AiServiceUnavailableException("El servicio de IA respondió en un formato inesperado.", ex);
            }
        }
    }

    private static async Task<ProblemOut?> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ProblemOut>(AiServiceClient.Json, cancellationToken);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            return null;
        }
    }

    private sealed record ProblemOut(string? Code, string? Detail);

    private sealed record ActionOut(string Title, string Description, string ExpectedImpact, string Effort, string Horizon);

    private sealed record RecommendationOut(string Summary, IReadOnlyList<string> RootCauses, IReadOnlyList<ActionOut> Actions, string Model);

    private sealed record SummaryOut(IReadOnlyList<string> Paragraphs, IReadOnlyList<string> Priorities, string Model);

    private sealed record AnomaliesOut(IReadOnlyList<AiAnomaly> Anomalies);
}
