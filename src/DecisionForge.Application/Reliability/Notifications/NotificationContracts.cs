using DecisionForge.Domain.Notifications;

namespace DecisionForge.Application.Reliability.Notifications;

public interface INotificationStore
{
    Task<bool> CreateIfAbsentAsync(
        Notification notification,
        CancellationToken cancellationToken);

    Task<bool> HasEmailBeenDeliveredAsync(
        Guid sourceOutboxMessageId,
        CancellationToken cancellationToken);

    Task<bool> MarkEmailDeliveredAsync(
        Guid sourceOutboxMessageId,
        DateTimeOffset deliveredAt,
        CancellationToken cancellationToken);
}

public sealed record NotificationDelivery(
    Guid DeliveryId,
    string RecipientEmail,
    string Subject,
    string Body);

public interface INotificationSender
{
    Task SendAsync(NotificationDelivery delivery, CancellationToken cancellationToken);
}
