using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Opsynex.Application.Homologation;
using Opsynex.Application.Insights;
using Opsynex.Infrastructure.AiService;

namespace Opsynex.UnitTests.Diagnosis;

public sealed class AiInsightsClientTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        public string? Path { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri!.AbsolutePath;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static (AiInsightsClient Client, StubHandler Handler) Create(HttpStatusCode status, string body, bool enabled = true)
    {
        var handler = new StubHandler(status, body);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://ai.test/") };
        return (new AiInsightsClient(http, Options.Create(new AiServiceOptions { Enabled = enabled })), handler);
    }

    private static readonly AiSummaryRequest Request = new(
        new AiCompanyContext("Manufacturing", null, "República Dominicana", "DOP", 2024),
        null, null, [], [], [], 0m, 1_000m);

    [Fact]
    public async Task Summary_SendsSnakeCaseJson_AndReadsTheResponse()
    {
        var (client, handler) = Create(HttpStatusCode.OK,
            """{"paragraphs":["Uno."],"priorities":["A","B","C"],"model":"claude-opus-5-5"}""");

        var result = await client.GenerateSummaryAsync(Request, TestContext.Current.CancellationToken);

        handler.Path.ShouldBe("/executive-summary");
        var body = handler.RequestBody.ShouldNotBeNull();
        body.ShouldContain("\"total_estimated_loss\":0");
        System.Text.Json.JsonDocument.Parse(body).RootElement.GetProperty("company").GetProperty("country").GetString()
            .ShouldBe("República Dominicana");
        result.Paragraphs.ShouldBe(["Uno."]);
        result.Model.ShouldBe("claude-opus-5-5");
    }

    [Fact]
    public async Task Recommendation_MapsActions()
    {
        var (client, _) = Create(HttpStatusCode.OK,
            """{"summary":"S","root_causes":["c1"],"actions":[{"title":"T","description":"D","expected_impact":"E","effort":"low","horizon":"long"}],"model":"m"}""");
        var request = new AiRecommendationRequest(Request.Company,
            new AiProblem("X", "t", "d", "High", "Profitability", "MarginLoss", 1m, null, new Dictionary<string, object?>(), "base", []));

        var result = await client.GenerateRecommendationAsync(request, TestContext.Current.CancellationToken);

        result.Summary.ShouldBe("S");
        result.RootCauses.ShouldBe(["c1"]);
        result.Actions.Single().ShouldBe(new AiAction("T", "D", "E", "low", "long"));
    }

    [Fact]
    public async Task NotConfigured_IsADistinctError()
    {
        var (client, _) = Create(HttpStatusCode.ServiceUnavailable,
            """{"code":"llm_not_configured","detail":"Falta ANTHROPIC_API_KEY."}""");
        var error = await Should.ThrowAsync<AiNotConfiguredException>(() => client.GenerateSummaryAsync(Request, TestContext.Current.CancellationToken));
        error.Message.ShouldBe("Falta ANTHROPIC_API_KEY.");
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, """{"code":"llm_rate_limited","detail":"Límite alcanzado."}""", "Límite alcanzado.")]
    [InlineData(HttpStatusCode.BadGateway, "no es json", "El servicio de IA respondió 502.")]
    public async Task OtherFailures_AreUnavailable(HttpStatusCode status, string body, string message)
    {
        var (client, _) = Create(status, body);
        var error = await Should.ThrowAsync<AiServiceUnavailableException>(() => client.GenerateSummaryAsync(Request, TestContext.Current.CancellationToken));
        error.Message.ShouldBe(message);
    }

    [Fact]
    public async Task Disabled_DoesNotCall()
    {
        var (client, handler) = Create(HttpStatusCode.OK, "{}", enabled: false);
        await Should.ThrowAsync<AiServiceUnavailableException>(() => client.GenerateSummaryAsync(Request, TestContext.Current.CancellationToken));
        handler.Path.ShouldBeNull();
    }
}
