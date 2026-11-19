using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Opsynex.Domain.Common;

/// <summary>Clave de comparación de textos: minúsculas, sin acentos, "&amp;" → "y", sin signos y con espacios simples.</summary>
public static partial class TextKey
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (ch == '&')
            {
                builder.Append(" y ");
            }
            else if (char.IsLetterOrDigit(ch) || ch == '%')
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                builder.Append(' ');
            }
        }

        return MultipleSpaces().Replace(builder.ToString(), " ").Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultipleSpaces();
}
