using DecisionForge.Application.Reliability;
using DecisionForge.Domain.Audit;
using DecisionForge.Domain.Outbox;
using DecisionForge.Domain.ValueObjects;
using DecisionForge.Infrastructure.Reliability;
using Npgsql;

namespace DecisionForge.Infrastructure.IntegrationTests.Reliability;

[Collection(ReliabilityTestGroup.Name)]
public sealed class PostgresReliabilityStoreTests(ReliabilityContainerFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset _now = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid _aggregateId = Guid.Parse("11111111-1111-4111-8111-111111111111");

    public Task InitializeAsync()
    {
        return fixture.ResetAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task BusinessStateAuditAndOutboxCommitAndRollbackAtomically()
    {
        await CommitBusinessChangeAsync(
            Guid.Parse("22222222-2222-4222-8222-222222222222"),
            Guid.Parse("33333333-3333-4333-8333-333333333333"),
            Guid.Parse("44444444-4444-4444-8444-444444444444"),
            commit: true);
        await CommitBusinessChangeAsync(
            Guid.Parse("55555555-5555-4555-8555-555555555555"),
            Guid.Parse("66666666-6666-4666-8666-666666666666"),
            Guid.Parse("77777777-7777-4777-8777-777777777777"),
            commit: false);

        await using NpgsqlConnection connection = new(fixture.ConnectionString);
        await connection.OpenAsync();
        Assert.Equal(1, await CountAsync(connection, "phase12_business_records"));
        Assert.Equal(1, await CountAsync(connection, "audit_events"));
        Assert.Equal(1, await CountAsync(connection, "outbox_messages"));
    }

    [Fact]
    public async Task ConcurrentAppendsSerializePerAggregateAndChainVerifies()
    {
        Task first = AppendSingleAsync(Guid.Parse("22222222-2222-4222-8222-222222222222"));
        Task second = AppendSingleAsync(Guid.Parse("33333333-3333-4333-8333-333333333333"));
        await Task.WhenAll(first, second);
        await using NpgsqlDataSource source = NpgsqlDataSource.Create(fixture.ConnectionString);
        PostgresReliabilityStore store = new(source);

        IReadOnlyList<DecisionForge.Domain.Audit.AuditEvent> chain =
            await store.LoadAuditChainAsync("PurchaseRequest", _aggregateId, CancellationToken.None);

        Assert.Equal([1L, 2L], chain.Select(item => item.Sequence));
        Assert.True(AuditChainVerifier.Verify(chain).IsValid);
        Assert.Equal(chain[0].Hash, chain[1].PreviousHash);
    }

    [Fact]
    public async Task ClaimsRetryTerminalFailureIdempotentCompletionAndSafeCleanupUsePostgres()
    {
        Guid retryId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        Guid completeId = Guid.Parse("33333333-3333-4333-8333-333333333333");
        await AppendOutboxAsync(retryId, maximumAttempts: 2);
        await AppendOutboxAsync(completeId, maximumAttempts: 2);
        await using NpgsqlDataSource source = NpgsqlDataSource.Create(fixture.ConnectionString);
        PostgresReliabilityStore store = new(source);

        IReadOnlyList<DecisionForge.Application.Reliability.Outbox.OutboxWorkItem> claimed =
            await store.ClaimAsync(_now, 10, TimeSpan.FromMinutes(1), CancellationToken.None);
        DecisionForge.Application.Reliability.Outbox.OutboxWorkItem retry =
            Assert.Single(claimed, item => item.MessageId == retryId);
        DecisionForge.Application.Reliability.Outbox.OutboxWorkItem complete =
            Assert.Single(claimed, item => item.MessageId == completeId);
        await store.RecordFailureAsync(
            retry.MessageId, retry.LeaseToken, "temporary-failure", _now,
            _now.AddSeconds(5), CancellationToken.None);
        Assert.True(await store.CompleteAsync(
            complete.MessageId, complete.LeaseToken, _now.AddSeconds(1), CancellationToken.None));
        Assert.False(await store.CompleteAsync(
            complete.MessageId, complete.LeaseToken, _now.AddSeconds(2), CancellationToken.None));
        Assert.Empty(await store.ClaimAsync(
            _now.AddSeconds(4), 10, TimeSpan.FromMinutes(1), CancellationToken.None));

        DecisionForge.Application.Reliability.Outbox.OutboxWorkItem secondAttempt = Assert.Single(
            await store.ClaimAsync(
                _now.AddSeconds(5), 10, TimeSpan.FromMinutes(1), CancellationToken.None));
        DecisionForge.Application.Reliability.Outbox.OutboxFailureResult failed =
            await store.RecordFailureAsync(
                secondAttempt.MessageId, secondAttempt.LeaseToken, "terminal-failure",
                _now.AddSeconds(5), _now.AddSeconds(10), CancellationToken.None);
        Assert.True(failed.IsTerminal);

        Assert.Equal(1, await store.DeleteCompletedAsync(
            _now.AddDays(1), 10, CancellationToken.None));
        await using NpgsqlConnection connection = new(fixture.ConnectionString);
        await connection.OpenAsync();
        Assert.Equal("failed", await StatusAsync(connection, retryId));
        Assert.Equal(1, await CountAsync(connection, "outbox_messages"));
    }

    private async Task CommitBusinessChangeAsync(
        Guid businessId,
        Guid auditId,
        Guid outboxId,
        bool commit)
    {
        await using NpgsqlConnection connection = new(fixture.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await using (NpgsqlCommand business = new(
            "INSERT INTO phase12_business_records (id, state) VALUES (@id, 'submitted')",
            connection,
            transaction))
        {
            business.Parameters.AddWithValue("id", businessId);
            await business.ExecuteNonQueryAsync();
        }

        await PostgresReliabilityStore.AppendAsync(
            connection,
            transaction,
            [Request(auditId)],
            [Message(outboxId, 3)],
            CancellationToken.None);
        if (commit)
        {
            await transaction.CommitAsync();
        }
        else
        {
            await transaction.RollbackAsync();
        }
    }

    private async Task AppendSingleAsync(Guid auditId)
    {
        await using NpgsqlConnection connection = new(fixture.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await PostgresReliabilityStore.AppendAsync(
            connection, transaction, [Request(auditId)], [], CancellationToken.None);
        await transaction.CommitAsync();
    }

    private async Task AppendOutboxAsync(Guid messageId, int maximumAttempts)
    {
        await using NpgsqlConnection connection = new(fixture.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
        await PostgresReliabilityStore.AppendAsync(
            connection, transaction, [], [Message(messageId, maximumAttempts)], CancellationToken.None);
        await transaction.CommitAsync();
    }

    private static AuditAppendRequest Request(Guid eventId)
    {
        return new AuditAppendRequest(
            eventId,
            "PurchaseRequest",
            _aggregateId,
            "purchase-request.submitted",
            "user:99999999-9999-4999-8999-999999999999",
            _now,
            CorrelationId.Parse("phase12-integration"),
            AuditPayload.Create([new("status", "Submitted")]));
    }

    private static OutboxMessage Message(Guid id, int maximumAttempts)
    {
        return OutboxMessage.Create(
            id,
            "decisionforge.test.v1",
            AuditPayload.Create([new("status", "Submitted")]),
            _now,
            _now,
            maximumAttempts);
    }

    private static async Task<long> CountAsync(NpgsqlConnection connection, string table)
    {
        await using NpgsqlCommand command = new($"SELECT count(*) FROM {table}", connection);
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private static async Task<string> StatusAsync(NpgsqlConnection connection, Guid id)
    {
        await using NpgsqlCommand command = new(
            "SELECT status FROM outbox_messages WHERE id = @id",
            connection);
        command.Parameters.AddWithValue("id", id);
        return (string)(await command.ExecuteScalarAsync() ?? string.Empty);
    }
}
