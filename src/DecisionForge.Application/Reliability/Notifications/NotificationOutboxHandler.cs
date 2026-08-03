using System.Text.Json;
using DecisionForge.Application.Reliability.Outbox;
using DecisionForge.Domain.Notifications;

namespace DecisionForge.Application.Reliability.Notifications;

public sealed class NotificationOutboxHandler : IOutboxMessageHandler
{
    private readonly INotificationStore _store;
    private readonly INotificationSender _sender;
    private readonly TimeProvider _timeProvider;

    public NotificationOutboxHandler(
        INotificationStore store,
        INotificationSender sender,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _store = store;
        _sender = sender;
        _timeProvider = timeProvider;
    }

    public string MessageType => NotificationOutboxMessageFactory.MessageType;

    public async Task HandleAsync(
        OutboxWorkItem message,
        CancellationToken cancellationToken)
    {
        Notification notification = Parse(message);
        await _store.CreateIfAbsentAsync(notification, cancellationToken);
        if (await _store.HasEmailBeenDeliveredAsync(message.MessageId, cancellationToken))
        {
            return;
        }

        await _sender.SendAsync(
            new NotificationDelivery(
                message.MessageId,
                notification.EmailAddress,
                notification.Subject,
                notification.Body),
            cancellationToken);
        await _store.MarkEmailDeliveredAsync(
            message.MessageId,
            _timeProvider.GetUtcNow(),
            cancellationToken);
    }

    private static Notification Parse(OutboxWorkItem message)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(message.CanonicalPayload);
            JsonElement root = document.RootElement;
            return Notification.Create(
                Guid.Parse(root.GetProperty("notificationId").GetString()!),
                Guid.Parse(root.GetProperty("userId").GetString()!),
                message.MessageId,
                root.GetProperty("recipientEmail").GetString()!,
                root.GetProperty("subject").GetString()!,
                root.GetProperty("body").GetString()!,
                root.GetProperty("relativeLink").GetString()!,
                message.OccurredAt);
        }
        catch (Exception exception) when (
            exception is JsonException or KeyNotFoundException or FormatException)
        {
            throw new OutboxDeliveryException(
                "notification-payload-invalid",
                "Notification payload is invalid.");
        }
    }
}
