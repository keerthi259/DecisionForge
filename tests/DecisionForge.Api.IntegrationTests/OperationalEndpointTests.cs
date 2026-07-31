using System.Net;
using System.Net.Http.Json;
using DecisionForge.Api.Operations;

namespace DecisionForge.Api.IntegrationTests;

public sealed class OperationalEndpointTests
{
    [Fact]
    public async Task LivenessRemainsHealthyWhenDatabaseIsUnavailable()
    {
        await using ApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/health/live", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ReadinessIsUnavailableWhenDatabaseIsUnavailable()
    {
        await using ApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/health/ready", CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync(CancellationToken.None));
    }

    [Fact]
    public async Task VersionReturnsExplicitOperationalContract()
    {
        await using ApiFactory factory = new();
        using HttpClient client = factory.CreateClient();

        VersionResponse? response = await client.GetFromJsonAsync<VersionResponse>(
            "/version",
            CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("DecisionForge.Api", response.Application);
        Assert.False(string.IsNullOrWhiteSpace(response.Version));
    }
}
