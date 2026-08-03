using DecisionForge.Application.Reliability.Notifications;
using DecisionForge.Application.Reliability.Outbox;
using DecisionForge.Domain.Notifications;

namespace DecisionForge.Application.UnitTests.Reliability;

public sealed class NotificationOutboxHandlerTests
{
    private static readonly DateTimeOffset _now = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ValidPayloadSendsWithStableIdAndCreatesInAppNotification()
    {
        OutboxWorkItem item = Item();
        RecordingNotificationStore store = new();
        RecordingSender sender = new();
        NotificationOutboxHandler handler = new(store, sender, new FixedTimeProvider());

        await handler.HandleAsync(item, CancellationToken.None);
        await handler.HandleAsync(item, CancellationToken.None);

        Assert.Single(sender.Deliveries);
        Assert.All(sender.Deliveries, delivery => Assert.Equal(item.MessageId, delivery.DeliveryId));
        Assert.Equal(2, store.Calls);
        Assert.Single(store.Notifications.Select(value => value.Id).Distinct());
    }

    [Fact]
    public async Task InvalidPayloadFailsWithStableCodeAndCancellationPropagates()
    {
        NotificationOutboxHandler handler = new(
            new RecordingNotificationStore(),
            new RecordingSender(),
            new FixedTimeProvider());
        OutboxWorkItem invalid = Item() with { CanonicalPayload = "{\"userId\":\"bad\"}" };

        OutboxDeliveryException exception = await Assert.ThrowsAsync<OutboxDeliveryException>(
            () => handler.HandleAsync(invalid, CancellationToken.None));
        Assert.Equal("notification-payload-invalid", exception.ErrorCode);
    }

    private static OutboxWorkItem Item()
    {
        return new OutboxWorkItem(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            NotificationOutboxMessageFactory.MessageType,
            """
            {"body":"Approved.","notificationId":"22222222-2222-4222-8222-222222222222","recipientEmail":"requester@decisionforge.local","relativeLink":"/requests/33333333-3333-4333-8333-333333333333","subject":"Request approved","userId":"44444444-4444-4444-8444-444444444444"}
            """,
            _now,
            1,
            5,
            Guid.Parse("55555555-5555-4555-8555-555555555555"));
    }

    private sealed class RecordingNotificationStore : INotificationStore
    {
        private readonly HashSet<Guid> _sources = [];
        private readonly HashSet<Guid> _delivered = [];

        public int Calls { get; private set; }

        public List<Notification> Notifications { get; } = [];

        public Task<bool> CreateIfAbsentAsync(
            Notification notification,
            CancellationToken cancellationToken)
        {
            Calls++;
            Notifications.Add(notification);
            return Task.FromResult(_sources.Add(notification.SourceOutboxMessageId));
        }

        public Task<bool> HasEmailBeenDeliveredAsync(
            Guid sourceOutboxMessageId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_delivered.Contains(sourceOutboxMessageId));
        }

        public Task<bool> MarkEmailDeliveredAsync(
            Guid sourceOutboxMessageId,
            DateTimeOffset deliveredAt,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_delivered.Add(sourceOutboxMessageId));
        }
    }

    private sealed class RecordingSender : INotificationSender
    {
        public List<NotificationDelivery> Deliveries { get; } = [];

        public Task SendAsync(
            NotificationDelivery delivery,
            CancellationToken cancellationToken)
        {
            Deliveries.Add(delivery);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }
    }
}
