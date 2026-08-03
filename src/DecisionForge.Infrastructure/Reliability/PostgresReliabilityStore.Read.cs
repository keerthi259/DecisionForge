using DecisionForge.Domain.Audit;
using DecisionForge.Domain.ValueObjects;
using Npgsql;

namespace DecisionForge.Infrastructure.Reliability;

public sealed partial class PostgresReliabilityStore
{
    public async Task<IReadOnlyList<AuditEvent>> LoadAuditChainAsync(
        string aggregateType,
        Guid aggregateId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT event_id, sequence, aggregate_type, aggregate_id, event_type, actor,
                   occurred_at, correlation_id, payload, previous_hash, hash
            FROM audit_events
            WHERE aggregate_type = @aggregate_type AND aggregate_id = @aggregate_id
            ORDER BY sequence
            """;
        await using NpgsqlCommand command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("aggregate_type", aggregateType);
        command.Parameters.AddWithValue("aggregate_id", aggregateId);
        List<AuditEvent> events = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(AuditEvent.Restore(
                reader.GetGuid(0),
                reader.GetInt64(1),
                reader.GetString(2),
                reader.GetGuid(3),
                reader.GetString(4),
                reader.GetString(5),
                Utc(reader.GetDateTime(6)),
                CorrelationId.Parse(reader.GetString(7)),
                Payload(reader.GetString(8)),
                AuditHash.Parse(reader.GetString(9)),
                AuditHash.Parse(reader.GetString(10))));
        }

        return events;
    }
}
