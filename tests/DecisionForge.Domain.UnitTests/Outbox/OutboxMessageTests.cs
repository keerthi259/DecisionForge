using DecisionForge.Domain.Audit;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Outbox;

namespace DecisionForge.Domain.UnitTests.Outbox;

public sealed class OutboxMessageTests
{
    private static readonly DateTimeOffset _now = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AttemptFailureRetryAndIdempotentCompletionFollowControlledStates()
    {
        OutboxMessage message = Create(maximumAttempts: 2);
        message.StartAttempt(_now);

        Assert.False(message.RecordFailure("mailpit-unavailable", _now, _now.AddSeconds(5)));
        Assert.Equal(OutboxStatus.Pending, message.Status);
        Assert.Throws<DomainRuleException>(() => message.StartAttempt(_now.AddSeconds(4)));
        message.StartAttempt(_now.AddSeconds(5));
        Assert.True(message.Complete(_now.AddSeconds(6)));
        Assert.False(message.Complete(_now.AddSeconds(7)));
    }

    [Fact]
    public void MaximumAttemptFailureIsTerminalAndCannotBeReprocessed()
    {
        OutboxMessage message = Create(maximumAttempts: 1);
        message.StartAttempt(_now);

        Assert.True(message.RecordFailure("delivery-failed", _now, _now.AddSeconds(1)));
        Assert.Equal(OutboxStatus.Failed, message.Status);
        Assert.Equal("delivery-failed", message.LastErrorCode);
        Assert.Throws<DomainRuleException>(() => message.StartAttempt(_now.AddSeconds(2)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void MaximumAttemptBoundariesAreEnforced(int maximumAttempts)
    {
        Assert.Throws<DomainRuleException>(() => Create(maximumAttempts));
    }

    private static OutboxMessage Create(int maximumAttempts)
    {
        return OutboxMessage.Create(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            "decisionforge.test.v1",
            AuditPayload.Empty,
            _now,
            _now,
            maximumAttempts);
    }
}
