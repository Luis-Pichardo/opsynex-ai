using System.Globalization;
using System.Text;
using Opsynex.Application.Reports;

namespace Opsynex.Infrastructure.Reports;

/// <summary>
/// Gráfico de barras agrupadas (ingresos vs. costo total por año) en SVG para el PDF. Colores fijos
/// por entidad (GUIA_DE_DISENO.md §2.4): ingresos = slot 1, costo total = slot 2. Sin doble eje.
/// </summary>
internal static class TrendChartSvg
{
    public const int Width = 520;
    public const int Height = 220;
    private const int Left = 56;
    private const int Right = 12;
    private const int Top = 28;
    private const int Bottom = 28;

    public static string? Render(IReadOnlyList<ReportTrendPoint> points, string currency)
    {
        var data = points.Where(p => p.Revenue is not null || p.TotalCost is not null).ToList();
        if (data.Count == 0)
        {
            return null;
        }

        var max = data.Max(p => Math.Max(p.Revenue ?? 0m, p.TotalCost ?? 0m));
        if (max <= 0)
        {
            return null;
        }

        var scaleMax = NiceCeiling(max);
        var plotWidth = Width - Left - Right;
        var plotHeight = Height - Top - Bottom;
        var groupWidth = plotWidth / (double)data.Count;
        var barWidth = Math.Min(36, (groupWidth - 16) / 2);
        double Y(decimal value) => Top + plotHeight - (double)(value / scaleMax) * plotHeight;

        var svg = new StringBuilder();
        svg.Append(Inv($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{Width}\" height=\"{Height}\" viewBox=\"0 0 {Width} {Height}\" font-family=\"Inter\">"));

        // Leyenda arriba a la izquierda.
        svg.Append(Inv($"<rect x=\"{Left}\" y=\"4\" width=\"10\" height=\"10\" rx=\"2\" fill=\"{ReportTheme.Chart1}\"/>"));
        svg.Append(Inv($"<text x=\"{Left + 14}\" y=\"13\" font-size=\"10\" fill=\"{ReportTheme.Foreground}\">Ingresos</text>"));
        svg.Append(Inv($"<rect x=\"{Left + 74}\" y=\"4\" width=\"10\" height=\"10\" rx=\"2\" fill=\"{ReportTheme.Chart2}\"/>"));
        svg.Append(Inv($"<text x=\"{Left + 88}\" y=\"13\" font-size=\"10\" fill=\"{ReportTheme.Foreground}\">Costo total</text>"));

        // Grilla horizontal y eje Y.
        for (var i = 0; i <= 4; i++)
        {
            var value = scaleMax * i / 4;
            var y = Y(value);
            svg.Append(Inv($"<line x1=\"{Left}\" x2=\"{Width - Right}\" y1=\"{y:0.##}\" y2=\"{y:0.##}\" stroke=\"{(i == 0 ? ReportTheme.ChartAxis : ReportTheme.ChartGrid)}\" stroke-width=\"1\"/>"));
            svg.Append(Inv($"<text x=\"{Left - 6}\" y=\"{y + 3:0.##}\" font-size=\"9\" text-anchor=\"end\" fill=\"{ReportTheme.ForegroundMuted}\">{Escape(ReportFormat.MoneyCompact(value, currency))}</text>"));
        }

        for (var i = 0; i < data.Count; i++)
        {
            var point = data[i];
            var center = Left + (groupWidth * i) + (groupWidth / 2);
            Bar(svg, center - barWidth - 1, barWidth, point.Revenue, ReportTheme.Chart1, Y);
            Bar(svg, center + 1, barWidth, point.TotalCost, ReportTheme.Chart2, Y);
            svg.Append(Inv($"<text x=\"{center:0.##}\" y=\"{Height - 10}\" font-size=\"10\" text-anchor=\"middle\" fill=\"{ReportTheme.ForegroundSecondary}\">{point.Year}</text>"));
        }

        svg.Append("</svg>");
        return svg.ToString();
    }

    private static void Bar(StringBuilder svg, double x, double width, decimal? value, string color, Func<decimal, double> y)
    {
        if (value is not { } v || v <= 0)
        {
            return;
        }

        var top = y(v);
        var baseY = y(0);
        svg.Append(Inv($"<rect x=\"{x:0.##}\" y=\"{top:0.##}\" width=\"{width:0.##}\" height=\"{baseY - top:0.##}\" rx=\"2\" fill=\"{color}\"/>"));
    }

    /// <summary>Techo "redondo" para el eje (1, 2, 2,5 o 5 × 10^n).</summary>
    internal static decimal NiceCeiling(decimal value)
    {
        var exponent = Math.Floor(Math.Log10((double)value));
        var magnitude = (decimal)Math.Pow(10, exponent);
        foreach (var step in new[] { 1m, 2m, 2.5m, 5m, 10m })
        {
            if (step * magnitude >= value)
            {
                return step * magnitude;
            }
        }

        return 10m * magnitude;
    }

    private static string Inv(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private static string Escape(string text) => text.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal);
}
