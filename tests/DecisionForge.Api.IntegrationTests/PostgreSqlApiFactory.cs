using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

namespace DecisionForge.Api.IntegrationTests;

public class PostgreSqlApiFactory(
    string connectionString,
    IReadOnlyDictionary<string, string?>? settings = null)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:decisionforge", connectionString);
        builder.UseSetting("DecisionForge:Identity:Seeding:SeedRolesOnStartup", "false");
        builder.UseSetting("DecisionForge:Identity:Seeding:Demo:Enabled", "false");
        if (settings is null)
        {
            return;
        }

        foreach ((string key, string? value) in settings)
        {
            builder.UseSetting(key, value);
        }
    }
}

public class PostgreSqlApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;

    public PostgreSqlApiFixture()
        : this("decisionforge_phase14")
    {
    }

    protected PostgreSqlApiFixture(string databaseName)
    {
        _postgres = new PostgreSqlBuilder("postgres:18.4")
            .WithDatabase(databaseName)
            .WithUsername("decisionforge")
            .WithPassword("phase14-local-only")
            .Build();
    }

    internal PostgreSqlApiFactory Factory { get; private set; } = null!;

    public string ConnectionString => _postgres.GetConnectionString();

    public virtual async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        Factory = CreateFactory();
        using HttpClient client = CreateClient();
        using HttpResponseMessage response = await client.GetAsync(
            "/api/v1/openapi/v1.json",
            CancellationToken.None);
        response.EnsureSuccessStatusCode();
    }

    public virtual async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public HttpClient CreateClient()
    {
        return Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost", UriKind.Absolute),
            HandleCookies = true,
        });
    }

    public async Task<string> CreateEmptyDatabaseAsync(string databaseName)
    {
        NpgsqlConnectionStringBuilder adminBuilder = new(ConnectionString)
        {
            Database = "postgres",
        };
        await using NpgsqlConnection connection = new(adminBuilder.ConnectionString);
        await connection.OpenAsync();
        using NpgsqlCommandBuilder commandBuilder = new();
        await using NpgsqlCommand command = new(
            $"CREATE DATABASE {commandBuilder.QuoteIdentifier(databaseName)}",
            connection);
        await command.ExecuteNonQueryAsync();
        return new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Database = databaseName,
        }.ConnectionString;
    }

    protected virtual PostgreSqlApiFactory CreateFactory()
    {
        return new PostgreSqlApiFactory(ConnectionString);
    }

    protected static string Invariant(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}

[CollectionDefinition(Name)]
public sealed class PostgreSqlApiTestGroup : ICollectionFixture<PostgreSqlApiFixture>
{
    public const string Name = "Phase14PostgreSqlApi";
}
