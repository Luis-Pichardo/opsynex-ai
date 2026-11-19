using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Opsynex.Infrastructure.Persistence;

using Opsynex.IntegrationTests.Infrastructure;

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
        // Sin servicio de IA el API funciona igual (fallback): el estado general es Degraded y responde 200.
        json.RootElement.GetProperty("status").GetString().ShouldBe("Degraded");
        json.RootElement.GetProperty("service").GetString().ShouldBe("opsynex-api");
        var checks = json.RootElement.GetProperty("checks").EnumerateArray()
            .ToDictionary(c => c.GetProperty("name").GetString()!, c => c.GetProperty("status").GetString());
        checks["self"].ShouldBe("Healthy");
        checks["database"].ShouldBe("Healthy");
        checks["ai-service"].ShouldBe("Degraded");
    }

    [Fact]
    public void Api_UsesTestDatabase()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OpsynexDbContext>();

        // EF Core agrega "Application Name" al final de la cadena de SQL Server.
        context.Database.GetConnectionString().ShouldStartWith(factory.Database.ConnectionString);
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
