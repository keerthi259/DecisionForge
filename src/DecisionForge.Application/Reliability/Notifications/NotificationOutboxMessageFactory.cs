using DecisionForge.Domain.Audit;
using DecisionForge.Domain.Notifications;
using DecisionForge.Domain.Outbox;

namespace DecisionForge.Application.Reliability.Notifications;

public static class NotificationOutboxMessageFactory
{
    public const string MessageType = "decisionforge.notification.v1";

    public static OutboxMessage Create(
        Guid messageId,
        Guid notificationId,
        Guid userId,
        string recipientEmail,
        string subject,
        string body,
        string relativeLink,
        DateTimeOffset occurredAt,
        int maximumAttempts = 5)
    {
        Notification validated = Notification.Create(
            notificationId,
            userId,
            messageId,
            recipientEmail,
            subject,
            body,
            relativeLink,
            occurredAt);
        AuditPayload payload = AuditPayload.Create(
        [
            new("notificationId", notificationId.ToString("D")),
            new("userId", userId.ToString("D")),
            new("recipientEmail", validated.EmailAddress),
            new("subject", validated.Subject),
            new("body", validated.Body),
            new("relativeLink", validated.RelativeLink),
        ]);
        return OutboxMessage.Create(
            messageId,
            MessageType,
            payload,
            occurredAt,
            occurredAt,
            maximumAttempts);
    }
}
