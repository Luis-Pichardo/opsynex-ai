namespace Opsynex.Application.Abstractions.Diagnostics;

/// <summary>
/// Identificador de correlación de la operación actual (cabecera <c>X-Correlation-Id</c>). Lo fija
/// el middleware del API por petición y viaja en los logs, en los errores y en las llamadas al
/// servicio de IA (también en los trabajos de IA en segundo plano).
/// </summary>
public static class CorrelationContext
{
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>Propiedad de los logs estructurados.</summary>
    public const string LogProperty = "CorrelationId";

    private static readonly AsyncLocal<string?> Value = new();

    public static string? Current
    {
        get => Value.Value;
        set => Value.Value = value;
    }

    /// <summary>
    /// Acepta un id recibido solo si es corto y seguro para logs y cabeceras (letras, dígitos,
    /// <c>-</c>, <c>_</c> y <c>.</c>, hasta 64 caracteres); si no, devuelve <c>null</c>.
    /// </summary>
    public static string? Sanitize(string? value) =>
        !string.IsNullOrEmpty(value) && value.Length <= 64 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')
            ? value
            : null;

    public static string NewId() => Guid.NewGuid().ToString("N");
}

/// <summary>Enmascara datos personales antes de escribirlos en los logs.</summary>
public static class LogMask
{
    /// <summary>"ana.perez@empresa.com" → "a***@empresa.com".</summary>
    public static string Email(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return string.Empty;
        }

        var at = email.IndexOf('@', StringComparison.Ordinal);
        return at <= 0 ? "***" : $"{email[0]}***{email[at..]}";
    }
}
