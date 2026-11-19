namespace Opsynex.Infrastructure.AiService;

/// <summary>Opciones de la sección <c>AiService</c>.</summary>
public sealed class AiServiceOptions
{
    public const string SectionName = "AiService";

    /// <summary>Con <c>false</c> no se llama al servicio: se usa siempre el fallback local.</summary>
    public bool Enabled { get; set; } = true;

    public string BaseUrl { get; set; } = "http://localhost:8001";

    /// <summary>Clave interna compartida (cabecera X-Internal-Api-Key).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Tiempo máximo total de una llamada (incluidos los reintentos).</summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>Reintentos ante errores transitorios.</summary>
    public int MaxRetries { get; set; } = 2;
}
