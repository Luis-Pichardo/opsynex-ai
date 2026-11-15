using Microsoft.Extensions.Configuration;
using Opsynex.Api.Extensions;

namespace Opsynex.IntegrationTests.Api;

public sealed class CorsExtensionsTests
{
    [Fact]
    public void GetOrigins_ReadsArrayFromAppSettings()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cors:Origins:0"] = "http://localhost:5173/",
            ["Cors:Origins:1"] = "https://app.opsynex.ai",
        }).Build();

        CorsExtensions.GetOrigins(config).ShouldBe(["http://localhost:5173", "https://app.opsynex.ai"]);
    }

    [Fact]
    public void GetOrigins_ReadsCommaSeparatedEnvironmentValue()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cors:Origins"] = "http://localhost:5173, https://app.opsynex.ai ,",
        }).Build();

        CorsExtensions.GetOrigins(config).ShouldBe(["http://localhost:5173", "https://app.opsynex.ai"]);
    }

    [Fact]
    public void GetOrigins_ReturnsEmptyWhenNotConfigured()
    {
        CorsExtensions.GetOrigins(new ConfigurationBuilder().Build()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(404, "not_found")]
    [InlineData(422, "validation_failed")]
    [InlineData(500, "internal_error")]
    [InlineData(502, "internal_error")]
    public void CodeForStatus_MapsStatusToCode(int status, string code)
    {
        ProblemDetailsExtensions.CodeForStatus(status).ShouldBe(code);
    }
}
