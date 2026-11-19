using Microsoft.Extensions.Configuration;
using Opsynex.Application.Analysis;
using Opsynex.Domain.Engines.Kpis;
using Opsynex.Domain.Enums;

namespace Opsynex.UnitTests.Kpis;

public sealed class BenchmarkOptionsTests
{
    private static BenchmarkOptions FromAppSettings()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var configuration = new ConfigurationBuilder().AddJsonFile(path).Build();
        return configuration.GetSection(BenchmarkOptions.SectionName).Get<BenchmarkOptions>()!;
    }

    [Fact]
    public void AppSettings_HaveValidBenchmarksForBothIndustriesAndCurrencies()
    {
        var options = FromAppSettings();

        options.InvalidKeys().ShouldBeEmpty();
        var manufacturing = options.For(Industry.Manufacturing, "DOP");
        manufacturing[KpiCode.EbitdaMargin].ShouldBe(new BenchmarkRange(0m, 0.20m));
        manufacturing.ShouldContainKey(KpiCode.RevenuePerEmployee);
        options.For(Industry.Services, "USD")[KpiCode.RevenuePerEmployee].Best.ShouldBe(300_000m);
    }

    [Fact]
    public void MonetaryKpis_OnlyComeFromTheCurrency()
    {
        var options = new BenchmarkOptions();
        options.Industries["Manufacturing"] = new() { ["RevenuePerEmployee"] = new() { Worst = 1, Best = 2 }, ["NetMargin"] = new() { Worst = 0, Best = 0.1m } };
        options.Currencies["EUR"] = new() { ["NetMargin"] = new() { Worst = 9, Best = 10 } };

        var ranges = options.For(Industry.Manufacturing, "EUR");

        ranges.ShouldNotContainKey(KpiCode.RevenuePerEmployee);
        ranges[KpiCode.NetMargin].ShouldBe(new BenchmarkRange(0m, 0.1m));
        options.For(Industry.Manufacturing, "XXX").ShouldNotContainKey(KpiCode.RevenuePerEmployee);
    }

    [Fact]
    public void InvalidKeys_DetectsUnknownKpis()
    {
        var options = new BenchmarkOptions();
        options.Industries["Services"] = new() { ["NoExiste"] = new() };

        options.InvalidKeys().ShouldBe(["NoExiste"]);
    }
}
