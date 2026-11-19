using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Opsynex.Application.Insights;

namespace Opsynex.Infrastructure.AiService;

/// <summary>Trabajo de enriquecimiento con IA de una empresa.</summary>
/// <summary>Trabajo de IA; conserva el id de correlación de la petición que lo originó.</summary>
internal sealed record AiEnrichmentJob(Guid CompanyId, int? Year, bool Force, string? CorrelationId);

/// <summary>Cola en memoria de trabajos de IA (modo <see cref="AiMode.Background"/>).</summary>
internal sealed class AiEnrichmentQueue(AiJobTracker tracker) : IAiEnrichmentScheduler
{
    private readonly Channel<AiEnrichmentJob> _channel = Channel.CreateUnbounded<AiEnrichmentJob>(
        new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<AiEnrichmentJob> Reader => _channel.Reader;

    public Task ScheduleAsync(Guid companyId, int? year, bool force, CancellationToken cancellationToken)
    {
        tracker.Set(companyId, AiJobState.Pending);
        return _channel.Writer.WriteAsync(new AiEnrichmentJob(companyId, year, force, Application.Abstractions.Diagnostics.CorrelationContext.Current), cancellationToken).AsTask();
    }
}

/// <summary>Procesa la cola de IA en segundo plano, un trabajo a la vez, con su propio scope de DI.</summary>
internal sealed partial class AiEnrichmentWorker(
    AiEnrichmentQueue queue,
    IServiceScopeFactory scopes,
    ILogger<AiEnrichmentWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                // Sin HttpContext el filtro por empresa está desactivado: el servicio filtra por CompanyId.
                Application.Abstractions.Diagnostics.CorrelationContext.Current = job.CorrelationId;
                using var logScope = logger.BeginScope(new Dictionary<string, object?>
                {
                    [Application.Abstractions.Diagnostics.CorrelationContext.LogProperty] = job.CorrelationId,
                });
                await using var scope = scopes.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<AiEnrichmentService>();
                await service.EnrichAsync(job.CompanyId, job.Year, job.Force, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogJobFailed(logger, ex, job.CompanyId);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Falló el trabajo de IA de la empresa {CompanyId}.")]
    private static partial void LogJobFailed(ILogger logger, Exception exception, Guid companyId);
}

/// <summary>Ejecuta el enriquecimiento en la misma petición (modo <see cref="AiMode.Inline"/>, pruebas).</summary>
internal sealed class InlineAiEnrichmentScheduler(AiEnrichmentService service) : IAiEnrichmentScheduler
{
    public Task ScheduleAsync(Guid companyId, int? year, bool force, CancellationToken cancellationToken) =>
        service.EnrichAsync(companyId, year, force, cancellationToken);
}
