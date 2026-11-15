using System.Net;
using System.Text.Json;

namespace Opsynex.IntegrationTests.Api;

public sealed class HealthEndpointTests(OpsynexApiFactory factory) : IClassFixture<OpsynexApiFactory>
{
    [Fact]
    public async Task Health_ReturnsOkWithHealthyStatus()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        json.RootElement.GetProperty("status").GetString().ShouldBe("Healthy");
        json.RootElement.GetProperty("service").GetString().ShouldBe("opsynex-api");
        json.RootElement.GetProperty("checks").EnumerateArray()
            .Select(c => c.GetProperty("name").GetString())
            .ShouldContain("self");
    }

    [Fact]
    public async Task Health_AllowsConfiguredFrontendOrigin()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "http://localhost:5173");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.Headers.GetValues("Access-Control-Allow-Origin").ShouldContain("http://localhost:5173");
    }
}
