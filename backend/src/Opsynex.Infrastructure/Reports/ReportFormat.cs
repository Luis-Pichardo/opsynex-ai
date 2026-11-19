using System.Globalization;
using Opsynex.Domain.Engines.Kpis;

namespace Opsynex.Infrastructure.Reports;

/// <summary>Formatos del informe en español (mismos criterios que src/lib/format.ts del frontend).</summary>
internal static class ReportFormat
{
    public static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es");

    public const string Empty = "—";

    public static string Symbol(string currency) => currency.ToUpperInvariant() switch
    {
        "DOP" => "RD$",
        "USD" => "US$",
        "EUR" => "€",
        var code => code,
    };

    public static string Money(decimal? amount, string currency) =>
        amount is { } a ? $"{Symbol(currency)} {decimal.Round(a, 0).ToString("#,##0", Es)}" : Empty;

    /// <summary>Monto compacto: "RD$ 4,4 M", "RD$ 350 K".</summary>
    public static string MoneyCompact(decimal? amount, string currency)
    {
        if (amount is not { } a)
        {
            return Empty;
        }

        var abs = Math.Abs(a);
        var text = abs >= 1_000_000m ? (a / 1_000_000m).ToString("0.#", Es) + " M"
            : abs >= 1_000m ? (a / 1_000m).ToString("0", Es) + " K"
            : a.ToString("0", Es);
        return $"{Symbol(currency)} {text}";
    }

    public static string Percent(decimal? ratio) => ratio is { } r ? (r * 100m).ToString("0.0", Es) + " %" : Empty;

    public static string Number(decimal? value, int decimals = 0) =>
        value is { } v ? v.ToString(decimals == 0 ? "#,##0" : "#,##0." + new string('0', decimals), Es) : Empty;

    /// <summary>Con signo y "−" tipográfico: "+1,2", "−3,05".</summary>
    public static string Signed(decimal value, string format) =>
        (value < 0 ? "−" : value > 0 ? "+" : string.Empty) + Math.Abs(value).ToString(format, Es);

    public static string Date(DateTime value) =>
        value.ToString("d MMM yyyy", Es).Replace(".", string.Empty, StringComparison.Ordinal);

    /// <summary>Valor de un KPI según su unidad.</summary>
    public static string Kpi(decimal? value, KpiUnit unit, string currency) => value is null
        ? Empty
        : unit switch
        {
            KpiUnit.Ratio => Percent(value),
            KpiUnit.Times => value.Value.ToString("0.00", Es) + "×",
            KpiUnit.Money => Money(value, currency),
            KpiUnit.Days => value.Value.ToString("0", Es) + " días",
            _ => Number(value, 2),
        };

    /// <summary>Variación año contra año: puntos porcentuales para ratios, % para el resto.</summary>
    public static (string Text, bool? Favorable) Change(decimal? current, decimal? previous, KpiUnit unit, bool higherIsBetter)
    {
        if (current is not { } c || previous is not { } p)
        {
            return (Empty, null);
        }

        var delta = c - p;
        if (unit == KpiUnit.Ratio)
        {
            var pp = delta * 100m;
            return Math.Abs(pp) < 0.05m ? ("estable", null) : (Signed(pp, "0.0") + " pp", (delta > 0) == higherIsBetter);
        }

        if (p == 0)
        {
            return (Empty, null);
        }

        var percent = delta / Math.Abs(p) * 100m;
        return Math.Abs(percent) < 1m ? ("estable", null) : (Signed(percent, "0.0") + " %", (delta > 0) == higherIsBetter);
    }
}
