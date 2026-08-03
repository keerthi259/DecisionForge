using Npgsql;

namespace DecisionForge.Api.IntegrationTests;

[Collection(PostgreSqlApiTestGroup.Name)]
public sealed class PostgreSqlApiFactoryTests(PostgreSqlApiFixture fixture)
{
    [Fact]
    public async Task FactoryRunsRealApiAgainstPinnedPostgreSql()
    {
        using HttpClient client = fixture.CreateClient();
        using HttpResponseMessage openApi = await client.GetAsync(
            "/api/v1/openapi/v1.json",
            CancellationToken.None);
        await using NpgsqlConnection connection = new(fixture.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new("SHOW server_version", connection);
        string? serverVersion = (string?)await command.ExecuteScalarAsync();

        openApi.EnsureSuccessStatusCode();
        Assert.StartsWith("18.4", serverVersion, StringComparison.Ordinal);
    }
}
