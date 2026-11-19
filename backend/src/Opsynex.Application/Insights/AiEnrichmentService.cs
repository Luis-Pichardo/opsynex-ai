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
/// <item>Anomalías (sin LLM) en las ventas mensuales y, si hay módulo industrial, en las series diarias de
/// OEE y minutos de parada → problemas <c>ANOMALY_*</c> de severidad Low.</item>
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
    IRepository<ProductionData> production,
    IRepository<StandardData> standards,
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
        var settings = options.Value;

        // Las anomalías no necesitan estados financieros (una empresa puede tener solo datos industriales).
        if (settings.DetectAnomalies)
        {
            await DetectAnomaliesAsync(companyId, company.Currency, cancellationToken);
        }

        var kpis = await kpiResults.ListByCompanyAsync(companyId, cancellationToken);
        var current = year is null ? kpis.MaxBy(k => k.Year) : kpis.FirstOrDefault(k => k.Year == year);
        if (current is null)
        {
            return;
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

    /// <summary>Serie que se envía al detector y cómo se nombran sus anomalías.</summary>
    private sealed record AnomalySeries(string Kind, int Year, AiSeries Series);

    private async Task DetectAnomaliesAsync(Guid companyId, string currency, CancellationToken cancellationToken)
    {
        var sent = new List<AnomalySeries>();

        // Ventas mensuales (estados financieros).
        var months = await monthly.ListAsync(m => m.CompanyId == companyId, cancellationToken);
        sent.AddRange(months
            .Where(m => m.Revenue is not null)
            .GroupBy(m => m.Year)
            .Where(g => g.Count() >= 2)
            .Select(g => new AnomalySeries(RevenueKind, g.Key, new AiSeries(
                $"MonthlyRevenue{g.Key}",
                "las ventas",
                "Money",
                [.. g.OrderBy(m => m.Month).Select(m => new AiSeriesPoint(Period(m.Year, m.Month), m.Revenue!.Value))]))));

        // Series diarias de la planta (módulo industrial): OEE y minutos de parada.
        var productionRows = await production.ListAsync(p => p.CompanyId == companyId, cancellationToken);
        if (productionRows.Count > 0)
        {
            var standardRows = await standards.ListAsync(s => s.CompanyId == companyId, cancellationToken);
            var daily = Domain.Engines.Industrial.IndustrialKpiEngine.Daily(
                [.. productionRows.Select(p => new Domain.Engines.Industrial.ProductionRecord(
                    p.Date, p.MachineId, p.LineId, p.UnitsProduced, p.DowntimeMinutes, p.PlannedMinutes, p.CycleTime, p.GoodUnits, p.TotalUnits))],
                [.. standardRows.Select(s => new Domain.Engines.Industrial.StandardRecord(s.MachineId, s.StandardCycleTime, s.StandardCost, s.CostPerMinute))]);
            foreach (var year in daily.GroupBy(d => d.Date.Year).Where(g => g.Count() >= 2))
            {
                var oee = year.Where(d => d.Oee is not null).Select(d => new AiSeriesPoint(Day(d.Date), d.Oee!.Value)).ToList();
                if (oee.Count >= 2)
                {
                    sent.Add(new AnomalySeries(OeeKind, year.Key, new AiSeries($"DailyOee{year.Key}", "el OEE diario de la planta", "Ratio", oee)));
                }

                sent.Add(new AnomalySeries(DowntimeKind, year.Key, new AiSeries(
                    $"DailyDowntime{year.Key}",
                    "los minutos de parada diarios de la planta",
                    "Number",
                    [.. year.Select(d => new AiSeriesPoint(Day(d.Date), d.DowntimeMinutes))])));
            }
        }

        if (sent.Count == 0)
        {
            return;
        }

        var anomalies = await ai.DetectAnomaliesAsync(new AiAnomalyRequest([.. sent.Select(s => s.Series)], currency), cancellationToken);

        // Las anomalías previas de las mismas series (tipo y año) se reemplazan.
        var prefixes = sent.Select(s => $"{AnomalyPrefix}{s.Kind}_{s.Year:0000}").ToList();
        var years = sent.Select(s => s.Year).ToHashSet();
        var existing = (await problems.ListAsync(
                p => p.CompanyId == companyId && years.Contains(p.Year) && p.Code.StartsWith(AnomalyPrefix),
                cancellationToken))
            .Where(p => prefixes.Any(prefix => p.Code.StartsWith(prefix, StringComparison.Ordinal)))
            .ToDictionary(p => (p.Year, p.Code));

        foreach (var anomaly in anomalies)
        {
            var series = sent.FirstOrDefault(s => s.Series.Name == anomaly.Series);
            if (series is null)
            {
                continue;
            }

            var date = ParseDate(anomaly.Period);
            var code = series.Kind == RevenueKind
                ? $"{AnomalyPrefix}{RevenueKind}_{date:yyyy_MM}"
                : $"{AnomalyPrefix}{series.Kind}_{date:yyyy_MM_dd}";
            if (!existing.Remove((date.Year, code), out var problem))
            {
                problem = new Problem
                {
                    CompanyId = companyId,
                    Year = date.Year,
                    Code = code,
                    Title = string.Empty,
                    Description = string.Empty,
                    Recommendation = string.Empty,
                };
                await problems.AddAsync(problem, cancellationToken);
            }

            var (title, recommendation) = series.Kind switch
            {
                RevenueKind => (
                    $"Ventas mensuales atípicas ({MonthLabel(date)})",
                    "Revisa los registros contables del mes: confirma si el valor responde a un evento puntual (venta extraordinaria, " +
                    "estacionalidad, devoluciones) o a un error de carga, y corrígelo antes de volver a analizar si hace falta."),
                OeeKind => (
                    $"OEE diario atípico ({DayLabel(date)})",
                    "Revisa la bitácora de producción de ese día: averías, cambios de formato, falta de material o lotes con defectos. " +
                    "Si fue un error de registro, corrige el archivo de producción y vuelve a subirlo."),
                _ => (
                    $"Paradas diarias atípicas ({DayLabel(date)})",
                    "Identifica la causa de las paradas de ese día (avería mayor, falta de energía o de material) y registra el motivo; " +
                    "si se repite, inclúyela en el plan de mantenimiento preventivo."),
            };
            problem.Title = title;
            problem.Description = anomaly.Explanation;
            problem.Category = ProblemCategory.Operations;
            problem.Severity = Severity.Low;
            problem.ImpactCategory = ImpactCategory.Anomaly;
            problem.ImpactValue = null;
            problem.Recommendation = recommendation;
            problem.Evidence = new JsonObject
            {
                ["kind"] = "anomaly",
                ["series"] = series.Series.Name[..^4],
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

    private const string RevenueKind = "REVENUE";
    private const string OeeKind = "OEE";
    private const string DowntimeKind = "DOWNTIME";

    private static readonly string[] MonthNames = ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic"];

    private static string MonthLabel(DateOnly date) => $"{MonthNames[date.Month - 1]} {date.Year}";

    private static string DayLabel(DateOnly date) => $"{date.Day} {MonthNames[date.Month - 1]} {date.Year}";

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

    private static string Day(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Período de una anomalía: «2024-07» (mes) o «2024-07-15» (día).</summary>
    private static DateOnly ParseDate(string period)
    {
        var parts = period.Split('-');
        return new DateOnly(
            int.Parse(parts[0], CultureInfo.InvariantCulture),
            int.Parse(parts[1], CultureInfo.InvariantCulture),
            parts.Length > 2 ? int.Parse(parts[2], CultureInfo.InvariantCulture) : 1);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "IA generativa no configurada; empresa {CompanyId} sigue con las recomendaciones base.")]
    private static partial void LogNotConfigured(ILogger logger, Guid companyId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Servicio de IA no disponible para la empresa {CompanyId}: {Reason}")]
    private static partial void LogUnavailable(ILogger logger, Guid companyId, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error en el enriquecimiento con IA de la empresa {CompanyId}.")]
    private static partial void LogFailed(ILogger logger, Exception exception, Guid companyId);
}
