using System.Collections.Concurrent;

namespace Opsynex.Application.Insights;

public enum AiJobState
{
    Pending,
    Running,
    Completed,
    Failed,
}

/// <summary>Estado del último enriquecimiento con IA de una empresa.</summary>
/// <param name="State">Estado.</param>
/// <param name="ErrorCode">Si falló: <c>not_configured</c>, <c>unavailable</c> o <c>error</c>.</param>
/// <param name="Message">Mensaje para el usuario.</param>
/// <param name="UpdatedAt">Última actualización (UTC).</param>
public sealed record AiJobStatus(AiJobState State, string? ErrorCode, string? Message, DateTime UpdatedAt);

/// <summary>
/// Estado en memoria de los trabajos de IA por empresa (una instancia del API). Si se reinicia el
/// API se pierde, lo que solo afecta al aviso "generando…": los resultados están en la base de datos.
/// </summary>
public sealed class AiJobTracker(TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<Guid, AiJobStatus> _jobs = new();

    public AiJobStatus? Get(Guid companyId) => _jobs.GetValueOrDefault(companyId);

    public void Set(Guid companyId, AiJobState state, string? errorCode = null, string? message = null) =>
        _jobs[companyId] = new AiJobStatus(state, errorCode, message, timeProvider.GetUtcNow().UtcDateTime);
}

/// <summary>Programa el enriquecimiento con IA de una empresa (en segundo plano o en línea).</summary>
public interface IAiEnrichmentScheduler
{
    Task ScheduleAsync(Guid companyId, int? year, bool force, CancellationToken cancellationToken);
}
