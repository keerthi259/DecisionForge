using System.Data;
using DecisionForge.Application.Reliability;
using DecisionForge.Application.Reliability.Notifications;
using DecisionForge.Application.Reliability.Outbox;
using DecisionForge.Domain.Audit;
using DecisionForge.Domain.Outbox;
using DecisionForge.Domain.ValueObjects;
using Npgsql;
using NpgsqlTypes;

namespace DecisionForge.Infrastructure.Reliability;

public sealed partial class PostgresReliabilityStore : IOutboxStore, INotificationStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresReliabilityStore(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        _dataSource = dataSource;
    }

    public static async Task<IReadOnlyList<AuditEvent>> AppendAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyCollection<AuditAppendRequest> auditRequests,
        IReadOnlyCollection<OutboxMessage> outboxMessages,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(auditRequests);
        ArgumentNullException.ThrowIfNull(outboxMessages);
        if (transaction.Connection != connection)
        {
            throw new ArgumentException("Transaction does not belong to the supplied connection.", nameof(transaction));
        }

        List<AuditEvent> appended = [];
        foreach (IGrouping<(string AggregateType, Guid AggregateId), AuditAppendRequest> group in
                 auditRequests.GroupBy(request => (request.AggregateType, request.AggregateId))
                     .OrderBy(group => group.Key.AggregateType, StringComparer.Ordinal)
                     .ThenBy(group => group.Key.AggregateId))
        {
            AuditHead head = await LockHeadAsync(
                connection,
                transaction,
                group.Key.AggregateType,
                group.Key.AggregateId,
                cancellationToken);
            foreach (AuditAppendRequest request in group)
            {
                long sequence = head.Sequence + 1;
                AuditEvent auditEvent = AuditEvent.Create(
                    request.EventId,
                    sequence,
                    request.AggregateType,
                    request.AggregateId,
                    request.EventType,
                    request.Actor,
                    request.OccurredAt,
                    request.CorrelationId,
                    request.Payload,
                    head.Hash);
                await InsertAuditAsync(connection, transaction, auditEvent, cancellationToken);
                head = new AuditHead(sequence, auditEvent.Hash);
                appended.Add(auditEvent);
            }

            await UpdateHeadAsync(
                connection,
                transaction,
                group.Key.AggregateType,
                group.Key.AggregateId,
                head,
                cancellationToken);
        }

        foreach (OutboxMessage message in outboxMessages)
        {
            await InsertOutboxAsync(connection, transaction, message, cancellationToken);
        }

        return appended;
    }

    public async Task<IReadOnlyList<OutboxWorkItem>> ClaimAsync(
        DateTimeOffset now,
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH terminalized AS (
                UPDATE outbox_messages
                SET status = 'failed', last_error_code = 'outbox-lease-expired',
                    lease_token = NULL, locked_until = NULL
                WHERE status = 'processing' AND locked_until <= @now
                  AND attempts >= maximum_attempts
                RETURNING id
            ), candidates AS (
                SELECT id
                FROM outbox_messages
                WHERE attempts < maximum_attempts
                  AND ((status = 'pending' AND available_at <= @now)
                       OR (status = 'processing' AND locked_until <= @now))
                ORDER BY available_at, id
                FOR UPDATE SKIP LOCKED
                LIMIT @batch_size
            )
            UPDATE outbox_messages AS message
            SET status = 'processing',
                attempts = message.attempts + 1,
                lease_token = @lease_token,
                locked_until = @locked_until
            FROM candidates
            WHERE message.id = candidates.id
            RETURNING message.id, message.message_type, message.payload, message.occurred_at,
                      message.attempts, message.maximum_attempts, message.lease_token
            """;
        Guid leaseToken = Guid.CreateVersion7(now);
        await using NpgsqlConnection connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using NpgsqlCommand command = new(sql, connection, transaction);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("batch_size", batchSize);
        command.Parameters.AddWithValue("lease_token", leaseToken);
        command.Parameters.AddWithValue("locked_until", now + leaseDuration);
        List<OutboxWorkItem> messages = [];
        await using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                messages.Add(new OutboxWorkItem(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    Utc(reader.GetDateTime(3)),
                    reader.GetInt32(4),
                    reader.GetInt32(5),
                    reader.GetGuid(6)));
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return messages;
    }

    public async Task<bool> CompleteAsync(
        Guid messageId,
        Guid leaseToken,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE outbox_messages
            SET status = 'completed', completed_at = @completed_at,
                lease_token = NULL, locked_until = NULL, last_error_code = NULL
            WHERE id = @id AND status = 'processing' AND lease_token = @lease_token
            """;
        await using NpgsqlCommand command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("completed_at", completedAt);
        command.Parameters.AddWithValue("id", messageId);
        command.Parameters.AddWithValue("lease_token", leaseToken);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<OutboxFailureResult> RecordFailureAsync(
        Guid messageId,
        Guid leaseToken,
        string errorCode,
        DateTimeOffset failedAt,
        DateTimeOffset nextAvailableAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE outbox_messages
            SET status = CASE WHEN attempts >= maximum_attempts THEN 'failed' ELSE 'pending' END,
                available_at = CASE WHEN attempts >= maximum_attempts THEN @failed_at ELSE @next_available_at END,
                last_error_code = @error_code, lease_token = NULL, locked_until = NULL
            WHERE id = @id AND status = 'processing' AND lease_token = @lease_token
            RETURNING status, attempts
            """;
        await using NpgsqlCommand command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("failed_at", failedAt);
        command.Parameters.AddWithValue("next_available_at", nextAvailableAt);
        command.Parameters.AddWithValue("error_code", errorCode);
        command.Parameters.AddWithValue("id", messageId);
        command.Parameters.AddWithValue("lease_token", leaseToken);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("The outbox lease is no longer current.");
        }

        return new OutboxFailureResult(
            string.Equals(reader.GetString(0), "failed", StringComparison.Ordinal),
            reader.GetInt32(1));
    }

    public async Task<int> DeleteCompletedAsync(
        DateTimeOffset completedBefore,
        int batchSize,
        CancellationToken cancellationToken)
    {
        const string sql = """
            DELETE FROM outbox_messages AS message
            USING (
                SELECT id FROM outbox_messages
                WHERE status = 'completed' AND completed_at < @completed_before
                ORDER BY completed_at, id
                FOR UPDATE SKIP LOCKED
                LIMIT @batch_size
            ) AS expired
            WHERE message.id = expired.id
            """;
        await using NpgsqlCommand command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("completed_before", completedBefore);
        command.Parameters.AddWithValue("batch_size", batchSize);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<AuditHead> LockHeadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string aggregateType,
        Guid aggregateId,
        CancellationToken cancellationToken)
    {
        const string insertSql = """
            INSERT INTO audit_aggregate_heads (aggregate_type, aggregate_id, last_sequence, last_hash)
            VALUES (@aggregate_type, @aggregate_id, 0, @zero_hash)
            ON CONFLICT (aggregate_type, aggregate_id) DO NOTHING
            """;
        await using (NpgsqlCommand insert = new(insertSql, connection, transaction))
        {
            insert.Parameters.AddWithValue("aggregate_type", aggregateType);
            insert.Parameters.AddWithValue("aggregate_id", aggregateId);
            insert.Parameters.AddWithValue("zero_hash", AuditHash.Zero.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        const string selectSql = """
            SELECT last_sequence, last_hash
            FROM audit_aggregate_heads
            WHERE aggregate_type = @aggregate_type AND aggregate_id = @aggregate_id
            FOR UPDATE
            """;
        await using NpgsqlCommand select = new(selectSql, connection, transaction);
        select.Parameters.AddWithValue("aggregate_type", aggregateType);
        select.Parameters.AddWithValue("aggregate_id", aggregateId);
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Audit aggregate head could not be locked.");
        }

        return new AuditHead(reader.GetInt64(0), AuditHash.Parse(reader.GetString(1)));
    }

    private static async Task InsertAuditAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AuditEvent auditEvent,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO audit_events
                (event_id, sequence, aggregate_type, aggregate_id, event_type, actor,
                 occurred_at, correlation_id, payload, previous_hash, hash)
            VALUES
                (@event_id, @sequence, @aggregate_type, @aggregate_id, @event_type, @actor,
                 @occurred_at, @correlation_id, @payload, @previous_hash, @hash)
            """;
        await using NpgsqlCommand command = new(sql, connection, transaction);
        command.Parameters.AddWithValue("event_id", auditEvent.Id);
        command.Parameters.AddWithValue("sequence", auditEvent.Sequence);
        command.Parameters.AddWithValue("aggregate_type", auditEvent.AggregateType);
        command.Parameters.AddWithValue("aggregate_id", auditEvent.AggregateId);
        command.Parameters.AddWithValue("event_type", auditEvent.EventType);
        command.Parameters.AddWithValue("actor", auditEvent.Actor);
        command.Parameters.AddWithValue("occurred_at", auditEvent.OccurredAt);
        command.Parameters.AddWithValue("correlation_id", auditEvent.CorrelationId.Value);
        command.Parameters.Add("payload", NpgsqlDbType.Jsonb).Value = auditEvent.Payload.CanonicalJson;
        command.Parameters.AddWithValue("previous_hash", auditEvent.PreviousHash.Value);
        command.Parameters.AddWithValue("hash", auditEvent.Hash.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpdateHeadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string aggregateType,
        Guid aggregateId,
        AuditHead head,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE audit_aggregate_heads
            SET last_sequence = @sequence, last_hash = @hash
            WHERE aggregate_type = @aggregate_type AND aggregate_id = @aggregate_id
            """;
        await using NpgsqlCommand command = new(sql, connection, transaction);
        command.Parameters.AddWithValue("sequence", head.Sequence);
        command.Parameters.AddWithValue("hash", head.Hash.Value);
        command.Parameters.AddWithValue("aggregate_type", aggregateType);
        command.Parameters.AddWithValue("aggregate_id", aggregateId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertOutboxAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO outbox_messages
                (id, message_type, payload, occurred_at, available_at, status,
                 attempts, maximum_attempts)
            VALUES
                (@id, @message_type, @payload, @occurred_at, @available_at, 'pending',
                 0, @maximum_attempts)
            """;
        await using NpgsqlCommand command = new(sql, connection, transaction);
        command.Parameters.AddWithValue("id", message.Id);
        command.Parameters.AddWithValue("message_type", message.MessageType);
        command.Parameters.Add("payload", NpgsqlDbType.Jsonb).Value = message.Payload.CanonicalJson;
        command.Parameters.AddWithValue("occurred_at", message.OccurredAt);
        command.Parameters.AddWithValue("available_at", message.AvailableAt);
        command.Parameters.AddWithValue("maximum_attempts", message.MaximumAttempts);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static AuditPayload Payload(string json)
    {
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(json);
        return AuditPayload.Create(document.RootElement.EnumerateObject().Select(
            property => new KeyValuePair<string, string>(property.Name, property.Value.GetString()!)));
    }

    private static DateTimeOffset Utc(DateTime value)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    private sealed record AuditHead(long Sequence, AuditHash Hash);
}
