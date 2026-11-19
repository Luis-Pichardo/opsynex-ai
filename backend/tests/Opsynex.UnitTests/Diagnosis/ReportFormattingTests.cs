using Opsynex.Application.Reports;
using Opsynex.Domain.Engines.Kpis;
using Opsynex.Infrastructure.Reports;

namespace Opsynex.UnitTests.Diagnosis;

public sealed class ReportFormattingTests
{
    [Fact]
    public void Money_UsesCurrencySymbolAndSpanishSeparators()
    {
        ReportFormat.Money(1_234_567.4m, "DOP").ShouldBe("RD$ 1.234.567");
        ReportFormat.MoneyCompact(4_397_941m, "DOP").ShouldBe("RD$ 4,4 M");
        ReportFormat.MoneyCompact(350_000m, "USD").ShouldBe("US$ 350 K");
        ReportFormat.Money(null, "DOP").ShouldBe("—");
    }

    [Theory]
    [InlineData(0.1861, 0.1975, KpiUnit.Ratio, true, "−1,1 pp", false)]
    [InlineData(0.8533, 0.8420, KpiUnit.Ratio, false, "+1,1 pp", false)]
    [InlineData(1.80, 1.50, KpiUnit.Times, true, "+20,0 %", true)]
    [InlineData(0.20, 0.2001, KpiUnit.Ratio, true, "estable", null)]
    public void Change_IsPpForRatiosAndPercentOtherwise(double current, double previous, KpiUnit unit, bool higherIsBetter, string text, bool? favorable)
    {
        var (change, isFavorable) = ReportFormat.Change((decimal)current, (decimal)previous, unit, higherIsBetter);
        change.ShouldBe(text);
        isFavorable.ShouldBe(favorable);
    }

    [Fact]
    public void Kpi_FormatsByUnit()
    {
        ReportFormat.Kpi(0.1861m, KpiUnit.Ratio, "DOP").ShouldBe("18,6 %");
        ReportFormat.Kpi(1.854m, KpiUnit.Times, "DOP").ShouldBe("1,85×");
        ReportFormat.Kpi(35.2m, KpiUnit.Days, "DOP").ShouldBe("35 días");
        ReportFormat.Kpi(12_116_066.67m, KpiUnit.Money, "DOP").ShouldBe("RD$ 12.116.067");
    }

    [Theory]
    [InlineData(181_741_000, 200_000_000)]
    [InlineData(95, 100)]
    [InlineData(2_100, 2_500)]
    [InlineData(4_900, 5_000)]
    public void ChartAxis_UsesRoundCeilings(double max, double ceiling) =>
        TrendChartSvg.NiceCeiling((decimal)max).ShouldBe((decimal)ceiling);

    [Fact]
    public void TrendChart_HasLegendBarsAndYears()
    {
        var svg = TrendChartSvg.Render(
            [new ReportTrendPoint(2023, 169_198_600m, 140_000_000m, 0.1975m), new ReportTrendPoint(2024, 181_741_000m, 155_000_000m, 0.1861m)],
            "DOP")!;

        svg.ShouldStartWith("<svg");
        svg.ShouldContain("Ingresos");
        svg.ShouldContain("Costo total");
        svg.ShouldContain(">2023<");
        svg.ShouldContain(">2024<");
        svg.Split("<rect").Length.ShouldBe(1 + 2 + 4); // 2 de la leyenda + 4 barras
        TrendChartSvg.Render([new ReportTrendPoint(2024, null, null, null)], "DOP").ShouldBeNull();
    }

    [Theory]
    [InlineData("Industrias Sintéticas S.R.L.", "industrias_sinteticas_s_r_l")]
    [InlineData("  ", "empresa")]
    [InlineData("Ñandú & Cía", "nandu_cia")]
    public void Slug_IsSafeForFileNames(string name, string slug) => ReportService.Slug(name).ShouldBe(slug);
}
