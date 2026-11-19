using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opsynex.Application.Abstractions.Persistence;
using Opsynex.Application.Analysis;
using Opsynex.Application.Common.Exceptions;
using Opsynex.Application.Homologation;
using Opsynex.Domain.Engines.Diagnosis;
using Opsynex.Domain.Engines.Kpis;
using Opsynex.Domain.Engines.Scoring;
using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;

namespace Opsynex.Application.Insights;

/// <summary>
/// Paso opcional del análisis con IA:
/// <list type="number">
/// <item>Anomalías en las ventas mensuales (sin LLM) → problemas <c>ANOMALY_*</c> de severidad Low.</item>
/// <item>Resumen ejecutivo del año con Claude → <see cref="ExecutiveSummary"/>.</item>
/// <item>Recomendaciones de IA de los problemas High y Medium → <c>Problem.AiRecommendation</c>.</item>
/// </list>
/// No regenera si los datos de entrada no cambiaron (hash). Nunca lanza: registra el resultado en
/// <see cref="AiJobTracker"/> y la plataforma sigue con las recomendaciones base.
/// </summary>
public sealed partial class AiEnrichmentService(
    ICompanyRepository companies,
    ICompanyYearRepository<KpiResult> kpiResults,
    ICompanyYearRepository<Score> scores,
    ICompanyYearRepository<FinancialData> financialData,
    ICompanyYearRepository<ExecutiveSummary> summaries,
    IRepository<Problem> problems,
    IRepository<MonthlyFinancialData> monthly,
    IAiInsightsClient ai,
    IUnitOfWork unitOfWork,
    IOptions<AiOptions> options,
    AiJobTracker tracker,
    TimeProvider timeProvider,
    ILogger<AiEnrichmentService> logger)
{
    public const string AnomalyPrefix = "ANOMALY_";

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Para el hash, los decimales se normalizan (181741000 = 181741000.00): un valor recién
    /// calculado y el mismo leído de SQL Server tienen distinta escala y darían otro hash.
    /// </summary>
    private static readonly JsonSerializerOptions HashJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new NormalizedDecimalConverter() },
    };

    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es");

    public async Task EnrichAsync(Guid companyId, int? year, bool force, CancellationToken cancellationToken)
    {
        tracker.Set(companyId, AiJobState.Running);
        try
        {
            await RunAsync(companyId, year, force, cancellationToken);
            tracker.Set(companyId, AiJobState.Completed);
        }
        catch (AiNotConfiguredException ex)
        {
            LogNotConfigured(logger, companyId);
            tracker.Set(companyId, AiJobState.Failed, "not_configured", ex.Message);
        }
        catch (AiServiceUnavailableException ex)
        {
            LogUnavailable(logger, companyId, ex.Message);
            tracker.Set(companyId, AiJobState.Failed, "unavailable", ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailed(logger, ex, companyId);
            tracker.Set(companyId, AiJobState.Failed, "error", "Ocurrió un error al generar el análisis con IA.");
        }
    }

    private async Task RunAsync(Guid companyId, int? year, bool force, CancellationToken cancellationToken)
    {
        var company = await companies.GetByIdAsync(companyId, cancellationToken) ?? throw new NotFoundException("la empresa", companyId);
        var kpis = await kpiResults.ListByCompanyAsync(companyId, cancellationToken);
        var current = year is null ? kpis.MaxBy(k => k.Year) : kpis.FirstOrDefault(k => k.Year == year);
        if (current is null)
        {
            return;
        }

        var settings = options.Value;
        if (settings.DetectAnomalies)
        {
            await DetectAnomaliesAsync(companyId, company.Currency, cancellationToken);
        }

        var context = new AiCompanyContext(company.Industry.ToString(), company.Sector, CountryFor(company.Currency, settings),
            company.Currency, current.Year);
        var previous = kpis.FirstOrDefault(k => k.Year == current.Year - 1);
        var yearProblems = await problems.ListAsync(p => p.CompanyId == companyId && p.Year == current.Year, cancellationToken);

        // Primero el resumen: si la IA generativa no está configurada, falla aquí y no se hacen más llamadas.
        await SummaryAsync(companyId, context, current, previous, yearProblems, force, cancellationToken);
        await RecommendationsAsync(context, current, previous, yearProblems, settings, force, cancellationToken);
    }

    // --- Anomalías ---

    private async Task DetectAnomaliesAsync(Guid companyId, string currency, CancellationToken cancellationToken)
    {
        var months = await monthly.ListAsync(m => m.CompanyId == companyId, cancellationToken);
        var series = months
            .Where(m => m.Revenue is not null)
            .GroupBy(m => m.Year)
            .Where(g => g.Count() >= 2)
            .Select(g => new AiSeries(
                $"MonthlyRevenue{g.Key}",
                "Las ventas",
                "Money",
                [.. g.OrderBy(m => m.Month).Select(m => new AiSeriesPoint(Period(m.Year, m.Month), m.Revenue!.Value))]))
            .ToList();
        if (series.Count == 0)
        {
            return;
        }

        var anomalies = await ai.DetectAnomaliesAsync(new AiAnomalyRequest(series, currency), cancellationToken);
        var years = series.Select(s => int.Parse(s.Name["MonthlyRevenue".Length..], CultureInfo.InvariantCulture)).ToHashSet();
        var existing = (await problems.ListAsync(
                p => p.CompanyId == companyId && years.Contains(p.Year) && p.Code.StartsWith(AnomalyPrefix),
                cancellationToken))
            .ToDictionary(p => (p.Year, p.Code));

        foreach (var anomaly in anomalies)
        {
            var (y, m) = ParsePeriod(anomaly.Period);
            var code = $"{AnomalyPrefix}REVENUE_{y:0000}_{m:00}";
            if (!existing.Remove((y, code), out var problem))
            {
                problem = new Problem
                {
                    CompanyId = companyId,
                    Year = y,
                    Code = code,
                    Title = string.Empty,
                    Description = string.Empty,
                    Recommendation = string.Empty,
                };
                await problems.AddAsync(problem, cancellationToken);
            }

            var month = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Utc).ToString("MMM yyyy", Es).Replace(".", string.Empty, StringComparison.Ordinal);
            problem.Title = $"Ventas mensuales atípicas ({month})";
            problem.Description = anomaly.Explanation;
            problem.Category = ProblemCategory.Operations;
            problem.Severity = Severity.Low;
            problem.ImpactCategory = ImpactCategory.Anomaly;
            problem.ImpactValue = null;
            problem.Recommendation =
                "Revisa los registros contables del mes: confirma si el valor responde a un evento puntual (venta extraordinaria, " +
                "estacionalidad, devoluciones) o a un error de carga, y corrígelo antes de volver a analizar si hace falta.";
            problem.Evidence = new JsonObject
            {
                ["kind"] = "anomaly",
                ["series"] = "MonthlyRevenue",
                ["period"] = anomaly.Period,
                ["value"] = anomaly.Value,
                ["expected"] = anomaly.Expected,
                ["score"] = anomaly.Score,
                ["direction"] = anomaly.Direction,
                ["method"] = anomaly.Method,
            };
        }

        // Las anomalías que ya no se detectan se eliminan.
        problems.RemoveRange(existing.Values);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // --- Resumen ejecutivo ---

    private async Task SummaryAsync(
        Guid companyId,
        AiCompanyContext context,
        KpiResult current,
        KpiResult? previous,
        IReadOnlyList<Problem> yearProblems,
        bool force,
        CancellationToken cancellationToken)
    {
        var score = await scores.GetByCompanyAndYearAsync(companyId, current.Year, cancellationToken);
        var previousScore = await scores.GetByCompanyAndYearAsync(companyId, current.Year - 1, cancellationToken);
        var financial = await financialData.GetByCompanyAndYearAsync(companyId, current.Year, cancellationToken);
        var ordered = DiagnosisEngine.Prioritize(yearProblems, p => p.Severity, p => p.ImpactValue).Take(8).ToList();

        var request = new AiSummaryRequest(
            context,
            score is null
                ? null
                : new AiScore(score.TotalScore, ScoreEngine.Label(score.Interpretation),
                    score.Components.ToDictionary(c => ScoreService.NameOf(c.Dimension), c => c.Value)),
            previousScore?.TotalScore,
            Kpis(current, previous, KpiCatalog.All.Select(d => d.Code)),
            [.. ordered.Select(p => new AiProblemBrief(p.Title, p.Severity.ToString(), p.ImpactValue, p.Description))],
            [.. current.Highlights.Select(h => $"{h.Title}: {h.Detail}")],
            yearProblems.Sum(p => p.ImpactValue ?? 0m),
            financial?.Revenue);

        var hash = Hash(request);
        var existing = await summaries.GetByCompanyAndYearAsync(companyId, current.Year, cancellationToken);
        if (!force && existing?.InputHash == hash)
        {
            return;
        }

        var result = await ai.GenerateSummaryAsync(request, cancellationToken);
        if (existing is null)
        {
            existing = new ExecutiveSummary { CompanyId = companyId, Year = current.Year, Model = result.Model, InputHash = hash };
            await summaries.AddAsync(existing, cancellationToken);
        }

        existing.Paragraphs = [.. result.Paragraphs];
        existing.Priorities = [.. result.Priorities];
        existing.Model = result.Model;
        existing.InputHash = hash;
        existing.GeneratedAt = timeProvider.GetUtcNow().UtcDateTime;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // --- Recomendaciones ---

    private async Task RecommendationsAsync(
        AiCompanyContext context,
        KpiResult current,
        KpiResult? previous,
        IReadOnlyList<Problem> yearProblems,
        AiOptions settings,
        bool force,
        CancellationToken cancellationToken)
    {
        var severities = settings.RecommendationSeverities
            .Select(s => Enum.TryParse<Severity>(s, true, out var v) ? v : (Severity?)null)
            .OfType<Severity>()
            .ToHashSet();
        var candidates = DiagnosisEngine.Prioritize(yearProblems.Where(p => severities.Contains(p.Severity)), p => p.Severity, p => p.ImpactValue)
            .Take(settings.MaxRecommendations)
            .ToList();

        foreach (var problem in candidates)
        {
            var request = new AiRecommendationRequest(context, ToAiProblem(problem, current, previous));
            var hash = Hash(request);
            if (!force && ParseRecommendation(problem.AiRecommendation)?.InputHash == hash)
            {
                continue;
            }

            var result = await ai.GenerateRecommendationAsync(request, cancellationToken);
            problem.AiRecommendation = JsonSerializer.Serialize(
                result with { InputHash = hash, GeneratedAt = timeProvider.GetUtcNow().UtcDateTime }, Json);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private static AiProblem ToAiProblem(Problem problem, KpiResult current, KpiResult? previous)
    {
        KpiCode? related = problem.Evidence["kpi"] is JsonValue kpi && Enum.TryParse<KpiCode>(kpi.ToString(), out var code) ? code : null;
        IEnumerable<KpiCode> codes = [.. related is { } r ? [r] : Array.Empty<KpiCode>(), KpiCode.EbitdaMargin, KpiCode.GrossMargin, KpiCode.CostEfficiency];
        var affected = problem.Evidence["impact"]?["affectedMarginPp"] is JsonValue pp ? pp.GetValue<decimal>() : (decimal?)null;
        return new AiProblem(
            problem.Code,
            problem.Title,
            problem.Description,
            problem.Severity.ToString(),
            problem.Category.ToString(),
            problem.ImpactCategory.ToString(),
            problem.ImpactValue,
            affected,
            Primitives(problem.Evidence),
            problem.Recommendation,
            Kpis(current, previous, codes.Distinct()));
    }

    private static List<AiKpi> Kpis(KpiResult current, KpiResult? previous, IEnumerable<KpiCode> codes) =>
        [.. codes
            .Select(KpiCatalog.Get)
            .Where(d => current.Get(d.Code) is not null || previous?.Get(d.Code) is not null)
            .Select(d => new AiKpi(d.Code.ToString(), d.Name, d.Unit.ToString(), current.Get(d.Code), previous?.Get(d.Code)))];

    /// <summary>Valores simples de la evidencia (sin el cálculo de impacto ni anidados).</summary>
    private static Dictionary<string, object?> Primitives(JsonObject evidence)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, node) in evidence)
        {
            if (node is JsonValue value)
            {
                result[key] = value.GetValueKind() switch
                {
                    JsonValueKind.Number => value.GetValue<decimal>(),
                    JsonValueKind.String => value.GetValue<string>(),
                    _ => null,
                };
            }
        }

        return result;
    }

    /// <summary>Recomendación de IA guardada (o <c>null</c> si no hay o no se puede leer).</summary>
    public static AiRecommendation? ParseRecommendation(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AiRecommendation>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Hash SHA-256 de la entrada (para no regenerar si no cambió).</summary>
    public static string Hash<T>(T request) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, HashJson))));

    private sealed class NormalizedDecimalConverter : System.Text.Json.Serialization.JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetDecimal();

        // Dividir entre 1,000…0 quita los ceros finales de la escala.
        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(value / 1.000000000000000000000000000000000m);
    }

    private static string CountryFor(string currency, AiOptions options) => currency.ToUpperInvariant() switch
    {
        "DOP" => "República Dominicana",
        _ => options.Country,
    };

    private static string Period(int year, int month) => string.Create(CultureInfo.InvariantCulture, $"{year:0000}-{month:00}");

    private static (int Year, int Month) ParsePeriod(string period)
    {
        var parts = period.Split('-');
        return (int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "IA generativa no configurada; empresa {CompanyId} sigue con las recomendaciones base.")]
    private static partial void LogNotConfigured(ILogger logger, Guid companyId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Servicio de IA no disponible para la empresa {CompanyId}: {Reason}")]
    private static partial void LogUnavailable(ILogger logger, Guid companyId, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error en el enriquecimiento con IA de la empresa {CompanyId}.")]
    private static partial void LogFailed(ILogger logger, Exception exception, Guid companyId);
}
