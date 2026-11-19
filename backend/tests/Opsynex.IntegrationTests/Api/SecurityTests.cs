using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Opsynex.Application.Abstractions.Diagnostics;
using Opsynex.Domain.Enums;
using Opsynex.IntegrationTests.Infrastructure;
using Opsynex.IntegrationTests.Uploads;

namespace Opsynex.IntegrationTests.Api;

/// <summary>Cabeceras de seguridad, id de correlación y compresión (etapa 13).</summary>
public sealed class SecurityTests(OpsynexApiFactory factory) : IClassFixture<OpsynexApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/health")]
    [InlineData("/api/v1/auth/me")]
    [InlineData("/api/v1/system/info")]
    public async Task Responses_CarrySecurityHeaders(string path)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path, Ct);

        var headers = response.Headers;
        headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        headers.GetValues("X-Frame-Options").ShouldBe(["DENY"]);
        headers.GetValues("Referrer-Policy").ShouldBe(["no-referrer"]);
        headers.GetValues("Content-Security-Policy").Single().ShouldContain("default-src 'none'");
        headers.GetValues("Content-Security-Policy").Single().ShouldContain("frame-ancestors 'none'");
        headers.Contains("Permissions-Policy").ShouldBeTrue();
        if (path.StartsWith("/api", StringComparison.Ordinal))
        {
            headers.CacheControl!.NoStore.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task CorrelationId_IsCreated_EchoedAndIncludedInErrors()
    {
        using var client = factory.CreateClient();

        using var created = await client.GetAsync("/api/v1/auth/me", Ct);
        var generated = created.Headers.GetValues(CorrelationContext.HeaderName).Single();
        generated.Length.ShouldBe(32);
        var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync(Ct)).RootElement;
        body.GetProperty("correlationId").GetString().ShouldBe(generated);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Add(CorrelationContext.HeaderName, "pedido-123.abc");
        using var echoed = await client.SendAsync(request, Ct);
        echoed.Headers.GetValues(CorrelationContext.HeaderName).Single().ShouldBe("pedido-123.abc");

        // Un valor inseguro (saltos de línea, demasiado largo) se reemplaza por uno nuevo.
        using var unsafeRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        unsafeRequest.Headers.TryAddWithoutValidation(CorrelationContext.HeaderName, new string('x', 100));
        using var replaced = await client.SendAsync(unsafeRequest, Ct);
        replaced.Headers.GetValues(CorrelationContext.HeaderName).Single().ShouldNotBe(new string('x', 100));
    }

    [Theory]
    [InlineData("gzip")]
    [InlineData("br")]
    public async Task JsonResponses_AreCompressed(string encoding)
    {
        var (client, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        using (client)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/companies");
            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue(encoding));
            using var response = await client.SendAsync(request, Ct);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.Content.Headers.ContentEncoding.ShouldBe([encoding]);
        }
    }

    [Theory]
    [InlineData("ana.perez@empresa.com", "a***@empresa.com")]
    [InlineData("sin-arroba", "***")]
    [InlineData(null, "")]
    public void LogMask_HidesEmails(string? email, string expected) => LogMask.Email(email).ShouldBe(expected);
}

/// <summary>Limitación de peticiones en el login (por IP) y en las cargas (por usuario).</summary>
public sealed class RateLimitingTests(RateLimitedApiFactory factory) : IClassFixture<RateLimitedApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Login_IsLimitedPerClient_With429AndRetryAfter()
    {
        using var client = factory.CreateClient();
        for (var i = 0; i < 3; i++)
        {
            (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "nadie@demo.test", password = "x" }, Ct))
                .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using var limited = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "nadie@demo.test", password = "x" }, Ct);
        var problem = await limited.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "rate_limited");
        problem.GetProperty("detail").GetString()!.ShouldContain("Espera");
        int.Parse(limited.Headers.GetValues("Retry-After").Single(), System.Globalization.CultureInfo.InvariantCulture).ShouldBeInRange(1, 60);

        // Los demás endpoints no se limitan.
        (await client.GetAsync("/health", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Uploads_AreLimitedPerUser()
    {
        // El login de esta prueba usa la IP de la prueba anterior: el usuario se autentica con un token directo.
        var companyId = await TestUsers.CreateCompanyAsync(factory.Database);
        var user = await TestUsers.CreateUserAsync(factory.Database, UserRole.Analyst, companyId);
        using var analyst = factory.CreateClient();
        analyst.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestUsers.IssueToken(factory, user));

        var bytes = await File.ReadAllBytesAsync(Path.Combine(UploadClient.SampleDirectory, "industrial", "estandares.xlsx"), Ct);
        for (var i = 0; i < 2; i++)
        {
            (await analyst.UploadBytesAsync(bytes, "estandares.xlsx")).StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        await (await analyst.UploadBytesAsync(bytes, "estandares.xlsx")).ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "rate_limited");
    }
}
