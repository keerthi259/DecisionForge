using DecisionForge.Domain.Audit;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.UnitTests.Audit;

public sealed class AuditChainTests
{
    private static readonly DateTimeOffset _occurredAt =
        new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GoldenHashMatchesPublishedCanonicalFormula()
    {
        AuditEvent auditEvent = First();

        Assert.Equal(
            "21e0a346d234f14d6bfef415828dc3975c6c74936cac7ea7f0bae78b41487ecc",
            auditEvent.Hash.Value);
    }

    [Fact]
    public void ValidChainPassesAndTamperingReportsFirstInvalidSequence()
    {
        AuditEvent first = First();
        AuditEvent second = AuditEvent.Create(
            Guid.Parse("44444444-4444-4444-8444-444444444444"),
            2,
            first.AggregateType,
            first.AggregateId,
            "purchase-request.submitted",
            first.Actor,
            _occurredAt.AddMinutes(1),
            first.CorrelationId,
            AuditPayload.Create([new("status", "Submitted")]),
            first.Hash);
        AuditEvent tampered = AuditEvent.Restore(
            second.Id,
            second.Sequence,
            second.AggregateType,
            second.AggregateId,
            second.EventType,
            second.Actor,
            second.OccurredAt,
            second.CorrelationId,
            AuditPayload.Create([new("status", "Rejected")]),
            second.PreviousHash,
            second.Hash);

        Assert.Equal(AuditChainVerificationResult.Valid, AuditChainVerifier.Verify([first, second]));
        AuditChainVerificationResult result = AuditChainVerifier.Verify([tampered, first]);
        Assert.False(result.IsValid);
        Assert.Equal(2, result.FirstInvalidSequence);
    }

    [Fact]
    public void SequenceGapAndBrokenPreviousHashReportExpectedSequence()
    {
        AuditEvent first = First();
        AuditEvent third = AuditEvent.Create(
            Guid.Parse("55555555-5555-4555-8555-555555555555"),
            3,
            first.AggregateType,
            first.AggregateId,
            "purchase-request.withdrawn",
            first.Actor,
            _occurredAt.AddMinutes(2),
            first.CorrelationId,
            AuditPayload.Empty,
            AuditHash.Zero);

        Assert.Equal(2, AuditChainVerifier.Verify([first, third]).FirstInvalidSequence);
        Assert.True(AuditChainVerifier.Verify([]).IsValid);
    }

    private static AuditEvent First()
    {
        return AuditEvent.Create(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            1,
            "PurchaseRequest",
            Guid.Parse("22222222-2222-4222-8222-222222222222"),
            "purchase-request.created",
            "user:33333333-3333-4333-8333-333333333333",
            _occurredAt,
            CorrelationId.Parse("corr-001"),
            AuditPayload.Create([new("z", "last"), new("a", "first")]),
            AuditHash.Zero);
    }
}
