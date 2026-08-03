using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Npgsql;
using Testcontainers.PostgreSql;

namespace DecisionForge.Infrastructure.IntegrationTests.Reliability;

public sealed class ReliabilityContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.4")
        .WithDatabase("decisionforge_phase12")
        .WithUsername("decisionforge")
        .WithPassword("phase12-local-only")
        .Build();
    private readonly IContainer _mailpit = new ContainerBuilder("axllent/mailpit:v1.30.5")
        .WithPortBinding(8025, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(
            request => request.ForPort(8025).ForPath("/api/v1/info")))
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public Uri MailpitAddress => new(
        $"http://127.0.0.1:{_mailpit.GetMappedPublicPort(8025)}",
        UriKind.Absolute);

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _mailpit.StartAsync());
        await ResetAsync();
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _mailpit.DisposeAsync().AsTask());
    }

    public async Task ResetAsync()
    {
        const string schema = """
            DROP TABLE IF EXISTS notifications;
            DROP TABLE IF EXISTS outbox_messages;
            DROP TABLE IF EXISTS audit_events;
            DROP TABLE IF EXISTS audit_aggregate_heads;
            DROP TABLE IF EXISTS phase12_business_records;

            CREATE TABLE phase12_business_records (
                id uuid PRIMARY KEY,
                state text NOT NULL
            );

            CREATE TABLE audit_aggregate_heads (
                aggregate_type varchar(64) NOT NULL,
                aggregate_id uuid NOT NULL,
                last_sequence bigint NOT NULL CHECK (last_sequence >= 0),
                last_hash char(64) NOT NULL,
                PRIMARY KEY (aggregate_type, aggregate_id)
            );

            CREATE TABLE audit_events (
                event_id uuid PRIMARY KEY,
                sequence bigint NOT NULL CHECK (sequence > 0),
                aggregate_type varchar(64) NOT NULL,
                aggregate_id uuid NOT NULL,
                event_type varchar(128) NOT NULL,
                actor varchar(128) NOT NULL,
                occurred_at timestamp with time zone NOT NULL,
                correlation_id varchar(128) NOT NULL,
                payload jsonb NOT NULL CHECK (jsonb_typeof(payload) = 'object'),
                previous_hash char(64) NOT NULL,
                hash char(64) NOT NULL,
                UNIQUE (aggregate_type, aggregate_id, sequence),
                FOREIGN KEY (aggregate_type, aggregate_id)
                    REFERENCES audit_aggregate_heads (aggregate_type, aggregate_id)
                    ON DELETE RESTRICT
            );

            CREATE TABLE outbox_messages (
                id uuid PRIMARY KEY,
                message_type varchar(128) NOT NULL,
                payload jsonb NOT NULL CHECK (jsonb_typeof(payload) = 'object'),
                occurred_at timestamp with time zone NOT NULL,
                available_at timestamp with time zone NOT NULL,
                status varchar(16) NOT NULL CHECK (status IN ('pending', 'processing', 'completed', 'failed')),
                attempts integer NOT NULL CHECK (attempts >= 0),
                maximum_attempts integer NOT NULL CHECK (maximum_attempts BETWEEN 1 AND 20),
                lease_token uuid NULL,
                locked_until timestamp with time zone NULL,
                completed_at timestamp with time zone NULL,
                last_error_code varchar(64) NULL
            );
            CREATE INDEX ix_outbox_pending_available
                ON outbox_messages (available_at, id)
                WHERE status IN ('pending', 'processing');

            CREATE TABLE notifications (
                id uuid PRIMARY KEY,
                user_id uuid NOT NULL,
                source_outbox_message_id uuid NOT NULL UNIQUE,
                email_address varchar(254) NOT NULL,
                subject varchar(160) NOT NULL,
                body varchar(1000) NOT NULL,
                relative_link varchar(512) NOT NULL,
                created_at timestamp with time zone NOT NULL,
                read_at timestamp with time zone NULL,
                email_delivered_at timestamp with time zone NULL
            );
            CREATE INDEX ix_notifications_user_read
                ON notifications (user_id, read_at, created_at DESC);
            """;
        await using NpgsqlConnection connection = new(ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(schema, connection);
        await command.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ReliabilityTestGroup : ICollectionFixture<ReliabilityContainerFixture>
{
    public const string Name = "Phase12Reliability";
}
