using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Opsynex.Application.Homologation;
using Opsynex.Domain.Enums;

namespace Opsynex.Infrastructure.AiService;

/// <summary>
/// Cliente tipado del servicio de IA. Los reintentos, el timeout y el circuit breaker los aplica
/// el pipeline de resiliencia del <see cref="HttpClient"/>. Cualquier fallo se traduce en
/// <see cref="AiServiceUnavailableException"/> para que el API use el fallback local.
/// </summary>
internal sealed class AiServiceClient(HttpClient http, IOptions<AiServiceOptions> options) : IAiServiceClient
{
    public const string ApiKeyHeader = "X-Internal-Api-Key";

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<IReadOnlyList<AiSuggestion>> SuggestMappingsAsync(
        IReadOnlyList<AiAccount> accounts,
        IReadOnlyList<AiKnownMapping> knownMappings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(knownMappings);
        if (!options.Value.Enabled)
        {
            throw new AiServiceUnavailableException("El servicio de IA está desactivado (AiService:Enabled = false).");
        }

        var request = new SuggestRequest(
            accounts.Select(a => new AccountIn(a.Id, a.Label, a.NormalizedLabel, a.SheetType.ToString(), a.Section, a.AccountCode)).ToList(),
            knownMappings.Select(k => new KnownMappingIn(k.NormalizedLabel, CategoryNames.ToSnakeCase(k.Category), null, k.Section)).ToList());

        try
        {
            using var response = await http.PostAsJsonAsync("homologation/suggest", request, Json, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new AiServiceUnavailableException($"El servicio de IA respondió {(int)response.StatusCode}.");
            }

            var body = await response.Content.ReadFromJsonAsync<SuggestResponse>(Json, cancellationToken)
                ?? throw new AiServiceUnavailableException("El servicio de IA respondió vacío.");
            return body.Suggestions
                .Select(s => new AiSuggestion(s.Id ?? string.Empty, CategoryNames.Parse(s.Category) ?? OpsynexCategory.Other, s.Confidence, s.Method))
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException
            or Polly.Timeout.TimeoutRejectedException or Polly.CircuitBreaker.BrokenCircuitException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new AiServiceUnavailableException($"No se pudo llamar al servicio de IA: {ex.Message}", ex);
        }
    }

    private sealed record AccountIn(string Id, string Label, string NormalizedLabel, string SheetType, string? Section, string? AccountCode);

    private sealed record KnownMappingIn(string NormalizedLabel, string Category, string? SheetType, string? Section);

    private sealed record SuggestRequest(IReadOnlyList<AccountIn> Accounts, IReadOnlyList<KnownMappingIn> KnownMappings);

    private sealed record SuggestionOut(string? Id, string Label, string Category, decimal Confidence, string Method);

    private sealed record SuggestResponse(IReadOnlyList<SuggestionOut> Suggestions);
}
