using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DecisionForge.ContractTests;

public sealed class OpenApiContractSnapshotTests
{
    [Fact]
    public async Task VersionOneOpenApiMatchesApprovedSnapshot()
    {
        await using OpenApiFactory factory = new();
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost", UriKind.Absolute),
        });
        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/openapi/v1.json",
            CancellationToken.None);
        string actual = await response.Content.ReadAsStringAsync(CancellationToken.None);
        response.EnsureSuccessStatusCode();
        string snapshotPath = Path.Combine(AppContext.BaseDirectory, "Snapshots", "openapi-v1.json");
        string expected = await File.ReadAllTextAsync(snapshotPath, CancellationToken.None);

        JsonNode? expectedDocument = JsonNode.Parse(expected);
        JsonNode? actualDocument = JsonNode.Parse(actual);
        expectedDocument?.AsObject().Remove("servers");
        actualDocument?.AsObject().Remove("servers");
        Assert.True(
            JsonNode.DeepEquals(expectedDocument, actualDocument),
            "The generated OpenAPI contract differs from the approved snapshot. "
                + "Review the change and deliberately update Snapshots/openapi-v1.json.");
    }

    private sealed class OpenApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting(
                "ConnectionStrings:decisionforge",
                "Host=127.0.0.1;Port=1;Database=contract;Username=test;Password=test;"
                    + "Timeout=1;Command Timeout=1;Pooling=false");
            builder.UseSetting("DecisionForge:Identity:Seeding:SeedRolesOnStartup", "false");
            builder.UseSetting("DecisionForge:Identity:Seeding:Demo:Enabled", "false");
        }
    }
}
