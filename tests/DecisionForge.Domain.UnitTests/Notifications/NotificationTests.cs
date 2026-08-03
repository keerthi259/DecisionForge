using DecisionForge.Domain.Common;
using DecisionForge.Domain.Notifications;

namespace DecisionForge.Domain.UnitTests.Notifications;

public sealed class NotificationTests
{
    [Fact]
    public void NotificationHasStableSourceAndIdempotentReadTransition()
    {
        DateTimeOffset now = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);
        Notification notification = Notification.Create(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            Guid.Parse("22222222-2222-4222-8222-222222222222"),
            Guid.Parse("33333333-3333-4333-8333-333333333333"),
            "requester@decisionforge.local",
            "Request approved",
            "Your request was approved.",
            "/requests/44444444-4444-4444-8444-444444444444",
            now);

        Assert.False(notification.IsRead);
        Assert.True(notification.MarkRead(now.AddMinutes(1)));
        Assert.False(notification.MarkRead(now.AddMinutes(2)));
        Assert.True(notification.IsRead);
    }

    [Theory]
    [InlineData("https://evil.example/path")]
    [InlineData("//evil.example/path")]
    [InlineData("")]
    public void ExternalOrEmptyLinksAreRejected(string link)
    {
        Assert.Throws<DomainRuleException>(() => Notification.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "user@example.com",
            "Subject", "Body", link, DateTimeOffset.UnixEpoch));
    }
}
