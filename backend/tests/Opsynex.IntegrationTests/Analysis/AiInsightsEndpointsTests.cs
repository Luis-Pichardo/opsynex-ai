using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Opsynex.Application.Analysis;
using Opsynex.Application.Homologation;
using Opsynex.Application.Insights;
using Opsynex.Domain.Enums;
using Opsynex.IntegrationTests.Infrastructure;
using Opsynex.IntegrationTests.Uploads;
using UglyToad.PdfPig;

namespace Opsynex.IntegrationTests.Analysis;

/// <summary>Cliente de IA simulado: ninguna prueba llama al servicio de IA ni a Claude.</summary>
public sealed class FakeAiInsightsClient : IAiInsightsClient
{
    public ConcurrentQueue<object> Requests { get; } = new();

    public Func<Exception?> Failure { get; set; } = () => null;

    public IReadOnlyList<AiAnomaly> Anomalies { get; set; } = [];

    private int _llmCalls;

    public int LlmCalls => Volatile.Read(ref _llmCalls);

    public Task<AiRecommendation> GenerateRecommendationAsync(AiRecommendationRequest request, CancellationToken cancellationToken)
    {
        Record(request);
        Interlocked.Increment(ref _llmCalls);
        return Task.FromResult(new AiRecommendation(
            $"Diagnóstico de IA para {request.Problem.Code}.",
            ["Costo de ventas creció más que los ingresos", "Precios sin ajustar"],
            [new AiAction("Renegocia con proveedores", "Descuentos por volumen.", "Recuperar 0,5 pp.", "medium", "short")],
            "claude-test",
            default,
            string.Empty));
    }

    public Task<AiSummaryResult> GenerateSummaryAsync(AiSummaryRequest request, CancellationToken cancellationToken)
    {
        Record(request);
        Interlocked.Increment(ref _llmCalls);
        return Task.FromResult(new AiSummaryResult(
            [$"La empresa obtuvo un score de {request.Score?.Total} en {request.Company.Year}.", "Segundo párrafo.", "Tercer párrafo."],
            ["Recuperar el margen", "Controlar la nómina", "Revisar gastos generales"],
            "claude-test"));
    }

    public Task<IReadOnlyList<AiAnomaly>> DetectAnomaliesAsync(AiAnomalyRequest request, CancellationToken cancellationToken)
    {
        Requests.Enqueue(request);
        return Task.FromResult(Anomalies);
    }

    private void Record(object request)
    {
        Requests.Enqueue(request);
        if (Failure() is { } error)
        {
            throw error;
        }
    }
}

/// <summary>API con la IA activa en modo Inline y el cliente de IA simulado.</summary>
public sealed class FakeAiApiFactory(DatabaseFixture database) : OpsynexApiFactory(database)
{
    public FakeAiInsightsClient Ai { get; } = new();

    protected override void ConfigureSettings(IWebHostBuilder builder)
    {
        builder.UseSetting("Ai:Enabled", "true");
        builder.UseSetting("Ai:Mode", "Inline");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAiInsightsClient>();
            services.AddSingleton<IAiInsightsClient>(Ai);
        });
    }
}

public sealed class AiInsightsEndpointsTests(FakeAiApiFactory factory) : IClassFixture<FakeAiApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<(HttpClient Analyst, HttpClient Client, Guid CompanyId)> CompanyWithBookAsync()
    {
        factory.Ai.Failure = () => null;
        factory.Ai.Anomalies = [];
        var companyId = await TestUsers.CreateCompanyAsync(factory.Database);
        await using (var context = factory.Database.CreateContext())
        {
            var company = await context.Companies.SingleAsync(c => c.Id == companyId, Ct);
            company.Currency = "DOP";
            await context.SaveChangesAsync(Ct);
        }

        var (analyst, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst, companyId);
        var (client, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Client, companyId);
        (await analyst.UploadSampleAsync("libro_sintetico_3a.xls")).StatusCode.ShouldBe(HttpStatusCode.Created);
        return (analyst, client, companyId);
    }

    [Fact]
    public async Task Analysis_GeneratesSummary_AndRecommendationsForMediumProblems()
    {
        var (analyst, client, _) = await CompanyWithBookAsync();
        using (analyst)
        using (client)
        {
            // Al subir el archivo: resumen del último año (2024 solo tiene problemas Low → sin recomendaciones).
            var summary = await (await client.GetAsync("/api/v1/ai/summary", Ct)).ReadAsync<AiSummaryDto>();
            summary.Status.ShouldBe("ready");
            summary.Year.ShouldBe(2024);
            summary.Paragraphs.Count.ShouldBe(3);
            summary.Paragraphs[0].ShouldContain("2024");
            summary.Priorities.ShouldBe(["Recuperar el margen", "Controlar la nómina", "Revisar gastos generales"]);
            summary.Model.ShouldBe("claude-test");
            summary.GeneratedAt.ShouldNotBeNull();

            // 2023 tiene problemas Medium: al regenerar ese año se crean sus recomendaciones de IA.
            var regenerated = await analyst.PostAsync("/api/v1/ai/regenerate?year=2023", null, Ct);
            regenerated.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            (await regenerated.Content.ReadFromJsonAsync<AiSummaryDto>(Json.Options, Ct))!.Status.ShouldBe("ready");

            var page = await (await client.GetAsync("/api/v1/problems?year=2023&severity=Medium", Ct)).ReadAsync<ProblemPage>();
            page.Items.ShouldNotBeEmpty();
            foreach (var item in page.Items)
            {
                var detail = await (await client.GetAsync($"/api/v1/problems/{item.Id}", Ct)).ReadAsync<ProblemDetailDto>();
                detail.AiRecommendation.ShouldNotBeNull();
                detail.AiRecommendation.Summary.ShouldBe($"Diagnóstico de IA para {item.Code}.");
                detail.AiRecommendation.RootCauses.Count.ShouldBe(2);
                detail.AiRecommendation.Actions.Single().Effort.ShouldBe("medium");
                detail.AiRecommendation.InputHash.Length.ShouldBe(64);
                detail.Recommendation.Length.ShouldBeGreaterThan(0); // la base se conserva
            }

            var low = await (await client.GetAsync("/api/v1/problems?year=2023&severity=Low", Ct)).ReadAsync<ProblemPage>();
            var lowDetail = await (await client.GetAsync($"/api/v1/problems/{low.Items[0].Id}", Ct)).ReadAsync<ProblemDetailDto>();
            lowDetail.AiRecommendation.ShouldBeNull();
        }
    }

    [Fact]
    public async Task SameInput_IsNotRegenerated_ButForceRegenerates()
    {
        var (analyst, client, _) = await CompanyWithBookAsync();
        using (analyst)
        using (client)
        {
            var calls = factory.Ai.LlmCalls;
            (await analyst.PostAsync("/api/v1/analysis/run", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
            factory.Ai.LlmCalls.ShouldBe(calls); // mismos datos → caché por hash

            (await analyst.PostAsync("/api/v1/ai/regenerate", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
            factory.Ai.LlmCalls.ShouldBe(calls + 1); // forzado: vuelve a pedir el resumen
        }
    }

    [Fact]
    public async Task Anomalies_BecomeLowProblems_AndSurviveTheRuleEngine()
    {
        var (analyst, client, _) = await CompanyWithBookAsync();
        using (analyst)
        using (client)
        {
            factory.Ai.Anomalies =
            [
                new AiAnomaly("MonthlyRevenue2024", "2024-07", 32_000_000m, 15_000_000m, 8.1m, "high", "robust_zscore",
                    "Las ventas de 2024-07 (RD$ 32.000.000) están muy por encima de lo habitual."),
            ];
            (await analyst.PostAsync("/api/v1/analysis/run", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

            var anomaly = (await (await client.GetAsync("/api/v1/problems?year=2024&pageSize=100", Ct)).ReadAsync<ProblemPage>())
                .Items.Single(p => p.Code.StartsWith("ANOMALY_", StringComparison.Ordinal));
            anomaly.Code.ShouldBe("ANOMALY_REVENUE_2024_07");
            anomaly.Severity.ShouldBe(Severity.Low);
            anomaly.ImpactCategory.ShouldBe(ImpactCategory.Anomaly);
            anomaly.ImpactValue.ShouldBeNull();
            anomaly.Title.ShouldBe("Ventas mensuales atípicas (jul 2024)");
            anomaly.Description.ShouldContain("muy por encima");

            // Con la misma anomalía, volver a analizar no la elimina ni la duplica.
            (await analyst.PostAsync("/api/v1/analysis/run", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
            var again = await (await client.GetAsync("/api/v1/problems?year=2024&pageSize=100", Ct)).ReadAsync<ProblemPage>();
            again.Items.Count(p => p.Code == "ANOMALY_REVENUE_2024_07").ShouldBe(1);

            // Si ya no se detecta, se elimina.
            factory.Ai.Anomalies = [];
            (await analyst.PostAsync("/api/v1/analysis/run", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
            var after = await (await client.GetAsync("/api/v1/problems?year=2024&pageSize=100", Ct)).ReadAsync<ProblemPage>();
            after.Items.ShouldNotContain(p => p.Code.StartsWith("ANOMALY_", StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData("not_configured")]
    [InlineData("unavailable")]
    public async Task WithoutAi_TheMvpKeepsWorking_AndTheStatusSaysWhy(string failure)
    {
        factory.Ai.Failure = () => failure == "not_configured"
            ? new AiNotConfiguredException("La IA generativa no está configurada.")
            : new AiServiceUnavailableException("No se pudo conectar con Claude.");
        var companyId = await TestUsers.CreateCompanyAsync(factory.Database);
        var (analyst, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst, companyId);
        using (analyst)
        {
            (await analyst.UploadSampleAsync("libro_sintetico_3a.xls")).StatusCode.ShouldBe(HttpStatusCode.Created);

            var summary = await (await analyst.GetAsync("/api/v1/ai/summary", Ct)).ReadAsync<AiSummaryDto>();
            summary.Status.ShouldBe(failure);
            summary.Paragraphs.ShouldBeEmpty();
            summary.Message.ShouldNotBeNullOrWhiteSpace();

            // El resto del MVP no depende de la IA.
            (await analyst.GetAsync("/api/v1/kpis", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
            (await (await analyst.GetAsync("/api/v1/problems", Ct)).ReadAsync<ProblemPage>()).TotalCount.ShouldBeGreaterThan(0);
            (await analyst.GetAsync("/api/v1/reports/executive", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        factory.Ai.Failure = () => null;
    }

    [Fact]
    public async Task Payloads_ContainOnlyAggregates_NeverPersonalData()
    {
        while (factory.Ai.Requests.TryDequeue(out _))
        {
        }

        var (analyst, client, _) = await CompanyWithBookAsync();
        using (analyst)
        using (client)
        {
            (await analyst.PostAsync("/api/v1/ai/regenerate?year=2023", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Accepted);

            var readable = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var payloads = factory.Ai.Requests.Select(r => JsonSerializer.Serialize(r, r.GetType(), readable)).ToList();
            payloads.ShouldContain(p => p.Contains("AiRecommendationRequest", StringComparison.Ordinal) || p.Contains("BaseRecommendation", StringComparison.Ordinal));
            var all = string.Join('\n', payloads);

            // El libro sintético trae una nómina con "EMPLEADO 001…" y cédulas "000-0000000-0".
            Regex.IsMatch(all, @"EMPLEADO\s+\d", RegexOptions.IgnoreCase).ShouldBeFalse(); // filas de la nómina
            Regex.IsMatch(all, @"\d{3}-\d{7}-\d").ShouldBeFalse();
            Regex.IsMatch(all, @"\d{3}-\d{5}-\d").ShouldBeFalse(); // RNC
            all.ShouldNotContain("REGPASCUAL", Case.Insensitive);
            all.ShouldNotContain("Payroll", Case.Insensitive);
            // Sí lleva el contexto del país y la moneda.
            all.ShouldContain("República Dominicana");
            all.ShouldContain("\"Currency\":\"DOP\"");
        }
    }

    [Fact]
    public async Task ExecutiveReport_IncludesTheAiSummary()
    {
        var (analyst, client, _) = await CompanyWithBookAsync();
        using (analyst)
        using (client)
        {
            var pdf = await (await client.GetAsync("/api/v1/reports/executive", Ct)).Content.ReadAsByteArrayAsync(Ct);
            using var document = PdfDocument.Open(pdf);
            var text = string.Join(' ', document.GetPages().SelectMany(p => p.GetWords()).Select(w => w.Text));
            text.ShouldContain("Resumen ejecutivo");
            text.ShouldContain("generado con IA");
            text.ShouldContain("Recuperar el margen");
        }
    }

    [Fact]
    public async Task Permissions()
    {
        var (analyst, client, _) = await CompanyWithBookAsync();
        var (admin, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Admin);
        using (analyst)
        using (client)
        using (admin)
        {
            (await client.GetAsync("/api/v1/ai/summary", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
            await (await client.PostAsync("/api/v1/ai/regenerate", null, Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
            await (await admin.GetAsync("/api/v1/ai/summary", Ct)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "ai.company_required");
            await (await client.GetAsync("/api/v1/ai/summary?year=1800", Ct)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "ai.invalid_year");
        }
    }
}

public sealed class AiDisabledTests(OpsynexApiFactory factory) : IClassFixture<OpsynexApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WithAiDisabled_SummarySaysSo_AndRegenerateIsRejected()
    {
        var companyId = await TestUsers.CreateCompanyAsync(factory.Database);
        var (analyst, _) = await TestUsers.CreateAuthenticatedClientAsync(factory, UserRole.Analyst, companyId);
        using (analyst)
        {
            (await analyst.UploadSampleAsync("libro_sintetico_2a.xls")).StatusCode.ShouldBe(HttpStatusCode.Created);
            var summary = await (await analyst.GetAsync("/api/v1/ai/summary", Ct)).ReadAsync<AiSummaryDto>();
            summary.Status.ShouldBe("disabled");
            await (await analyst.PostAsync("/api/v1/ai/regenerate", null, Ct)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "ai.disabled");
        }
    }
}
