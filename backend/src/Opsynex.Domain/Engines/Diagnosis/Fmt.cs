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

    public static decimal R(decimal value, int decimals = 4) => decimal.Round(value, decimals);

    private static string Signed(decimal value, string format) =>
        (value < 0 ? "−" : value > 0 ? "+" : string.Empty) + Math.Abs(value).ToString(format, Es);
}
