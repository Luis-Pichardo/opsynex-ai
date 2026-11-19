using System.Globalization;

namespace Opsynex.Domain.Engines.Diagnosis;

/// <summary>Formato de cifras en los textos del diagnóstico (español: "18,61 %", "−1,14 pp").</summary>
internal static class Fmt
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es");

    public static string Pct(decimal ratio) => (ratio * 100m).ToString("0.##", Es) + " %";

    public static string Percent(decimal percent) => Signed(percent, "0.##") + " %";

    public static string Pp(decimal points) => Signed(points, "0.##") + " pp";

    public static string Times(decimal value) => value.ToString("0.##", Es);

    /// <summary>Monto con el símbolo de la moneda: "RD$ 1.234.567".</summary>
    public static string Money(decimal amount, string? currency)
    {
        var symbol = currency?.ToUpperInvariant() switch
        {
            "DOP" => "RD$",
            "USD" => "US$",
            "EUR" => "€",
            null or "" => string.Empty,
            var code => code,
        };
        var text = decimal.Round(amount, 0).ToString("#,##0", Es);
        return symbol.Length == 0 ? text : $"{symbol} {text}";
    }

    /// <summary>Monto con dos decimales: "RD$ 125,40".</summary>
    public static string Money2(decimal amount, string? currency)
    {
        var whole = Money(0m, currency);
        var symbol = whole.Length > 1 ? whole[..^2] : string.Empty;
        var text = decimal.Round(amount, 2).ToString("#,##0.00", Es);
        return symbol.Length == 0 ? text : $"{symbol} {text}";
    }

    public static decimal R(decimal value, int decimals = 4) => decimal.Round(value, decimals);

    private static string Signed(decimal value, string format) =>
        (value < 0 ? "−" : value > 0 ? "+" : string.Empty) + Math.Abs(value).ToString(format, Es);
}
