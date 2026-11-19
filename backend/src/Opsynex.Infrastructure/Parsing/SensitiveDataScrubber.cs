using System.Text.RegularExpressions;

namespace Opsynex.Infrastructure.Parsing;

/// <summary>
/// Oculta datos personales o sensibles que pueden aparecer en las etiquetas de las notas
/// (números de cuenta bancaria, cédulas y RNC) antes de guardarlas.
/// </summary>
public static partial class SensitiveDataScrubber
{
    public const string Mask = "***";

    public static string Scrub(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        return LongNumber().Replace(text, m => m.Value.Count(char.IsDigit) >= 6 ? Mask : m.Value);
    }

    /// <summary>Indica si el texto parece contener una cédula dominicana (000-0000000-0).</summary>
    public static bool ContainsCedula(string? text) => text is not null && Cedula().IsMatch(text);

    // Secuencias de dígitos con guiones, puntos o espacios (cédulas, RNC, cuentas bancarias).
    [GeneratedRegex(@"\d[\d\-\.\s/]{4,}\d", RegexOptions.CultureInvariant)]
    private static partial Regex LongNumber();

    [GeneratedRegex(@"\b\d{3}-?\d{7}-?\d\b", RegexOptions.CultureInvariant)]
    private static partial Regex Cedula();
}
