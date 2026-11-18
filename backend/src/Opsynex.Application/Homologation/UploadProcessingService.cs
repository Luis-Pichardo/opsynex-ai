using Microsoft.Extensions.Logging;
using Opsynex.Application.Abstractions.Persistence;
using Opsynex.Domain.Entities;
using Opsynex.Domain.Enums;

namespace Opsynex.Application.Homologation;

/// <summary>
/// Análisis automático al subir un archivo (sin pasos manuales): homologa con las mejores
/// sugerencias y consolida. Desde la etapa 05 ejecutará también el <c>AnalysisPipeline</c>.
/// Si algo falla, la carga no se pierde: queda con <c>AnalysisStatus = Failed</c> y una advertencia.
/// </summary>
public sealed partial class UploadProcessingService(
    HomologationService homologation,
    IRepository<Upload> uploads,
    IUnitOfWork unitOfWork,
    ILogger<UploadProcessingService> logger)
{
    public async Task<ProcessingResult?> ProcessAsync(Guid uploadId, CancellationToken cancellationToken)
    {
        try
        {
            return await homologation.ProcessUploadAsync(uploadId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailure(logger, uploadId, ex);
            if (await uploads.GetByIdAsync(uploadId, cancellationToken) is { } upload)
            {
                upload.AnalysisStatus = AnalysisStatus.Failed;
                upload.Warnings = [.. upload.Warnings, new UploadIssue(
                    "analysis.failed",
                    "Los datos se guardaron, pero el análisis automático falló. Vuelve a analizar desde la homologación.")];
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Falló el análisis automático de la carga {UploadId}")]
    private static partial void LogFailure(ILogger logger, Guid uploadId, Exception exception);
}
