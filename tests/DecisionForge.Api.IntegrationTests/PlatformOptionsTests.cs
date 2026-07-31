using DecisionForge.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DecisionForge.Api.IntegrationTests;

public sealed class PlatformOptionsTests
{
    [Fact]
    public async Task MissingApplicationNameFailsStartupWithConfigurationPath()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [$"{PlatformOptions.SectionName}:ApplicationName"] = string.Empty,
                [$"{PlatformOptions.SectionName}:CorrelationHeaderName"] = "X-Correlation-ID",
            });
        builder.Services.AddDecisionForgePlatform(builder.Configuration);
        using IHost host = builder.Build();

        OptionsValidationException exception = await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync(CancellationToken.None));

        Assert.Contains(
            $"{PlatformOptions.SectionName}:ApplicationName is required.",
            exception.Failures);
    }
}
