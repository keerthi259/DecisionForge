using DecisionForge.Domain.Notifications;
using Npgsql;

namespace DecisionForge.Infrastructure.Reliability;

public sealed partial class PostgresReliabilityStore
{
    public async Task<bool> CreateIfAbsentAsync(
        Notification notification,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO notifications
                (id, user_id, source_outbox_message_id, email_address, subject, body,
                 relative_link, created_at, read_at)
            VALUES
                (@id, @user_id, @source_id, @email, @subject, @body,
                 @relative_link, @created_at, NULL)
            ON CONFLICT (source_outbox_message_id) DO NOTHING
            """;
        ArgumentNullException.ThrowIfNull(notification);
        await using NpgsqlCommand command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("id", notification.Id);
        command.Parameters.AddWithValue("user_id", notification.UserId);
        command.Parameters.AddWithValue("source_id", notification.SourceOutboxMessageId);
        command.Parameters.AddWithValue("email", notification.EmailAddress);
        command.Parameters.AddWithValue("subject", notification.Subject);
        command.Parameters.AddWithValue("body", notification.Body);
        command.Parameters.AddWithValue("relative_link", notification.RelativeLink);
        command.Parameters.AddWithValue("created_at", notification.CreatedAt);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<bool> HasEmailBeenDeliveredAsync(
        Guid sourceOutboxMessageId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT email_delivered_at IS NOT NULL
            FROM notifications
            WHERE source_outbox_message_id = @source_id
            """;
        await using NpgsqlCommand command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("source_id", sourceOutboxMessageId);
        object? result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }

    public async Task<bool> MarkEmailDeliveredAsync(
        Guid sourceOutboxMessageId,
        DateTimeOffset deliveredAt,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE notifications
            SET email_delivered_at = @delivered_at
            WHERE source_outbox_message_id = @source_id
              AND email_delivered_at IS NULL
            """;
        await using NpgsqlCommand command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("source_id", sourceOutboxMessageId);
        command.Parameters.AddWithValue("delivered_at", deliveredAt);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }
}
