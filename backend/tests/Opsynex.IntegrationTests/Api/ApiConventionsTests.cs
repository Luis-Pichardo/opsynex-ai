using System.Net;
using System.Text.Json;

using Opsynex.IntegrationTests.Infrastructure;

namespace Opsynex.IntegrationTests.Api;

public sealed class ApiConventionsTests(OpsynexApiFactory factory) : IClassFixture<OpsynexApiFactory>
{
    [Fact]
    public async Task VersionedRoute_ReturnsSystemInfo()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/system/info", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        json.RootElement.GetProperty("name").GetString().ShouldBe("Opsynex AI API");
        json.RootElement.GetProperty("environment").GetString().ShouldBe("Development");
    }

    [Fact]
    public async Task UnknownRoute_WithoutSession_Returns401()
    {
        // La política por defecto exige sesión: sin token no se revela qué rutas existen.
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/does-not-exist", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnknownRoute_WithSession_ReturnsProblemDetailsWithCode()
    {
        var (client, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, Opsynex.Domain.Enums.UserRole.Client);
        using (client)
        {
            using var response = await client.GetAsync(new Uri("/api/v1/does-not-exist", UriKind.Relative), TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            json.RootElement.GetProperty("status").GetInt32().ShouldBe(404);
            json.RootElement.GetProperty("code").GetString().ShouldBe("not_found");
            json.RootElement.TryGetProperty("traceId", out _).ShouldBeTrue();
        }
    }

    [Fact]
    public async Task OpenApiDocument_IsPublishedInDevelopment()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("/api/v1/system/info");
    }
}
