using Opsynex.Domain.Enums;

namespace Opsynex.Application.Homologation;

/// <summary>Cuenta enviada al servicio de IA. Solo etiquetas y contexto: nunca datos personales.</summary>
public sealed record AiAccount(string Id, string Label, string NormalizedLabel, SheetType SheetType, string? Section, string? AccountCode);

/// <summary>Mapeo ya confirmado por la empresa (el servicio lo usa con prioridad).</summary>
public sealed record AiKnownMapping(string NormalizedLabel, OpsynexCategory Category, string? Section);

public sealed record AiSuggestion(string Id, OpsynexCategory Category, decimal Confidence, string Method);

/// <summary>Cliente del servicio de IA (Python). Lanza <see cref="AiServiceUnavailableException"/> si no responde.</summary>
public interface IAiServiceClient
{
    Task<IReadOnlyList<AiSuggestion>> SuggestMappingsAsync(
        IReadOnlyList<AiAccount> accounts,
        IReadOnlyList<AiKnownMapping> knownMappings,
        CancellationToken cancellationToken);
}

/// <summary>El servicio de IA no está disponible (apagado, lento o con error): se usa el fallback local.</summary>
public sealed class AiServiceUnavailableException : Exception
{
    public AiServiceUnavailableException()
        : base("El servicio de IA no está disponible.")
    {
    }

    public AiServiceUnavailableException(string message)
        : base(message)
    {
    }

    public AiServiceUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
