using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Opsynex.IntegrationTests.Infrastructure;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Verifica el estado HTTP y el <c>code</c> del ProblemDetails.</summary>
    public static async Task<JsonElement> ShouldBeProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(status, body);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("code").GetString().ShouldBe(code, body);
        return document.RootElement.Clone();
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(status, body);
        return JsonSerializer.Deserialize<T>(body, Options)!;
    }
}
