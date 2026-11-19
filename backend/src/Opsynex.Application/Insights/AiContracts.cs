using System.Text.Json.Serialization;

namespace Opsynex.Application.Insights;

// Contratos con el servicio de IA (Python). Solo agregados y KPIs: nunca nombres, cédulas ni
// salarios individuales (la nómina solo aporta la cantidad de empleados).

public sealed record AiCompanyContext(string Industry, string? Sector, string Country, string Currency, int Year);

public sealed record AiKpi(string Code, string Name, string Unit, decimal? Value, decimal? Previous);

public sealed record AiProblem(
    string Code,
    string Title,
    string Description,
    string Severity,
    string Category,
    string ImpactCategory,
    decimal? ImpactValue,
    decimal? AffectedMarginPp,
    IReadOnlyDictionary<string, object?> Evidence,
    string BaseRecommendation,
    IReadOnlyList<AiKpi> RelatedKpis);

public sealed record AiRecommendationRequest(AiCompanyContext Company, AiProblem Problem);

public sealed record AiAction(string Title, string Description, string ExpectedImpact, string Effort, string Horizon);

/// <summary>Recomendación de IA (se guarda como JSON en <c>Problem.AiRecommendation</c>).</summary>
public sealed record AiRecommendation(
    string Summary,
    IReadOnlyList<string> RootCauses,
    IReadOnlyList<AiAction> Actions,
    string Model,
    DateTime GeneratedAt,
    string InputHash);

public sealed record AiScore(decimal Total, string Interpretation, IReadOnlyDictionary<string, decimal?> Components);

public sealed record AiProblemBrief(string Title, string Severity, decimal? ImpactValue, string Description);

public sealed record AiSummaryRequest(
    AiCompanyContext Company,
    AiScore? Score,
    decimal? PreviousScore,
    IReadOnlyList<AiKpi> Kpis,
    IReadOnlyList<AiProblemBrief> Problems,
    IReadOnlyList<string> Highlights,
    decimal? TotalEstimatedLoss,
    decimal? Revenue);

public sealed record AiSummaryResult(IReadOnlyList<string> Paragraphs, IReadOnlyList<string> Priorities, string Model);

public sealed record AiSeriesPoint(string Period, decimal Value);

public sealed record AiSeries(string Name, string Label, string Unit, IReadOnlyList<AiSeriesPoint> Points);

public sealed record AiAnomalyRequest(IReadOnlyList<AiSeries> Series, string Currency);

public sealed record AiAnomaly(
    string Series,
    string Period,
    decimal Value,
    decimal Expected,
    decimal? Score,
    string Direction,
    string Method,
    string Explanation);

/// <summary>Cliente de recomendaciones, resumen ejecutivo y anomalías del servicio de IA.</summary>
public interface IAiInsightsClient
{
    /// <summary>Lanza <see cref="AiNotConfiguredException"/> si falta la clave de Claude y
    /// <see cref="Homologation.AiServiceUnavailableException"/> ante cualquier otro fallo.</summary>
    Task<AiRecommendation> GenerateRecommendationAsync(AiRecommendationRequest request, CancellationToken cancellationToken);

    Task<AiSummaryResult> GenerateSummaryAsync(AiSummaryRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<AiAnomaly>> DetectAnomaliesAsync(AiAnomalyRequest request, CancellationToken cancellationToken);
}

/// <summary>El servicio de IA responde, pero la IA generativa no está configurada (sin ANTHROPIC_API_KEY).</summary>
public sealed class AiNotConfiguredException : Exception
{
    public AiNotConfiguredException()
        : base("La IA generativa no está configurada.")
    {
    }

    public AiNotConfiguredException(string message)
        : base(message)
    {
    }

    public AiNotConfiguredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Modo de ejecución del enriquecimiento con IA.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AiMode>))]
public enum AiMode
{
    /// <summary>En segundo plano: el análisis termina sin esperar a la IA (producción).</summary>
    Background,

    /// <summary>En la misma petición, después de guardar el análisis (pruebas).</summary>
    Inline,
}

/// <summary>Sección <c>Ai</c>: recomendaciones, resumen ejecutivo y anomalías.</summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Con <c>false</c> no se pide nada a la IA: la plataforma usa las recomendaciones base.</summary>
    public bool Enabled { get; set; } = true;

    public AiMode Mode { get; set; } = AiMode.Background;

    /// <summary>Severidades que reciben recomendación de IA.</summary>
    public IList<string> RecommendationSeverities { get; set; } = ["High", "Medium"];

    /// <summary>Tope de recomendaciones por análisis (controla el costo).</summary>
    public int MaxRecommendations { get; set; } = 10;

    /// <summary>Detectar anomalías en las ventas mensuales.</summary>
    public bool DetectAnomalies { get; set; } = true;

    /// <summary>País de las empresas para el contexto de las recomendaciones.</summary>
    public string Country { get; set; } = "República Dominicana";

    /// <summary>Tiempo máximo de una llamada al servicio de IA para Claude (segundos).</summary>
    public int TimeoutSeconds { get; set; } = 180;
}
