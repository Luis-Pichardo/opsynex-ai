using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Opsynex.Infrastructure.Parsing;

/// <summary>Normalización de etiquetas y lectura de números con formatos contables.</summary>
public static partial class TextNormalizer
{
    /// <summary>Clave de comparación: minúsculas, sin acentos, "&amp;" → "y", sin signos y con espacios simples.</summary>
    public static string Key(string? text)
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

    /// <summary>Clave sin plurales simples ("costos de ventas" → "costo de venta"), para comparar etiquetas.</summary>
    public static string SingularKey(string? text) =>
        string.Join(' ', Key(text).Split(' ').Select(w => w.Length > 3 && w.EndsWith('s') ? w[..^1] : w));

    /// <summary>
    /// Etiqueta limpia: sin "(nota N)", "(N.N)", "nota N", prefijos "Menos:" y "más:", ni espacios extra.
    /// Conserva mayúsculas y acentos.
    /// </summary>
    public static string CleanLabel(string? original)
    {
        if (string.IsNullOrWhiteSpace(original))
        {
            return string.Empty;
        }

        var text = NoteReference().Replace(original, " ");
        text = NumberReference().Replace(text, " ");
        text = LooseNoteReference().Replace(text, " ");
        text = MultipleSpaces().Replace(text, " ").Trim();
        text = Prefix().Replace(text, string.Empty);
        return text.Trim().TrimEnd(':').Trim();
    }

    /// <summary>La etiqueta resta (empieza con "Menos").</summary>
    public static bool IsSubtraction(string? original) => Key(original).StartsWith("menos", StringComparison.Ordinal);

    /// <summary>Número de nota referido en la etiqueta: "(nota 3)" → "3", "(9.1)" → "9.1".</summary>
    public static string? NoteNumber(string? original)
    {
        if (string.IsNullOrWhiteSpace(original))
        {
            return null;
        }

        var match = NoteReference().Match(original);
        if (!match.Success)
        {
            match = NumberReference().Match(original);
        }

        return match.Success ? match.Groups["n"].Value : null;
    }

    /// <summary>Número al inicio del título de una nota: "3. EFECTIVO" o "Nota 3 - Efectivo" → "3".</summary>
    public static string? LeadingNoteNumber(string? title)
    {
        var match = LeadingNumber().Match(title ?? string.Empty);
        return match.Success ? match.Groups["n"].Value : null;
    }

    /// <summary>
    /// Lee un número de una celda: número de Excel o texto como "1,234.56", "(1,234)", "RD$ 1,234",
    /// "1.234,56". Una celda vacía no es cero (devuelve <c>false</c>); un guion solo se lee como 0.
    /// </summary>
    public static bool TryParseNumber(object? cell, out decimal value)
    {
        value = 0;
        switch (cell)
        {
            case null:
                return false;
            case double d when double.IsFinite(d):
                value = decimal.Round((decimal)d, 4);
                return true;
            case int i:
                value = i;
                return true;
            case long l:
                value = l;
                return true;
            case decimal m:
                value = m;
                return true;
            case string s:
                return TryParseText(s, out value);
            default:
                return false;
        }
    }

    private static bool TryParseText(string raw, out decimal value)
    {
        value = 0;
        var text = raw.Trim();
        if (text.Length == 0)
        {
            return false;
        }

        if (text is "-" or "—" or "–")
        {
            return true;
        }

        var negative = false;
        if (text.StartsWith('(') && text.EndsWith(')'))
        {
            negative = true;
            text = text[1..^1];
        }

        text = Currency().Replace(text, string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);
        if (text.StartsWith('-'))
        {
            negative = !negative;
            text = text[1..];
        }

        var lastComma = text.LastIndexOf(',');
        var lastDot = text.LastIndexOf('.');
        if (lastComma > lastDot && text.Length - lastComma - 1 is 1 or 2)
        {
            // Formato europeo: 1.234,56
            text = text.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');
        }
        else
        {
            text = text.Replace(",", string.Empty, StringComparison.Ordinal);
        }

        if (!PlainNumber().IsMatch(text)
            || !decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value))
        {
            value = 0;
            return false;
        }

        if (negative)
        {
            value = -value;
        }

        return true;
    }

    [GeneratedRegex(@"\(\s*nota\s*(?<n>\d+(?:\.\d+)?)\s*\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NoteReference();

    [GeneratedRegex(@"\(\s*(?<n>\d+(?:\.\d+)?)\s*\)", RegexOptions.CultureInvariant)]
    private static partial Regex NumberReference();

    [GeneratedRegex(@"\bnotas?\s*(?<n>\d+(?:\.\d+)?)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LooseNoteReference();

    [GeneratedRegex(@"^(menos|m[aá]s)\s*:\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Prefix();

    [GeneratedRegex(@"^\s*(nota\s*)?(?<n>\d+(?:\.\d+)?)\s*[\.\-–:)]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LeadingNumber();

    [GeneratedRegex(@"RD\$|US\$|\$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Currency();

    [GeneratedRegex(@"^\d+(\.\d+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex PlainNumber();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultipleSpaces();
}
