using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.Audit;

public sealed record AuditChainVerificationResult(bool IsValid, long? FirstInvalidSequence)
{
    public static AuditChainVerificationResult Valid { get; } = new(true, null);

    public static AuditChainVerificationResult Invalid(long sequence)
    {
        return new AuditChainVerificationResult(false, sequence);
    }
}

public static class AuditChainVerifier
{
    public static AuditChainVerificationResult Verify(IEnumerable<AuditEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        long expectedSequence = 1;
        AuditHash expectedPreviousHash = AuditHash.Zero;
        foreach (AuditEvent auditEvent in events.OrderBy(item => item.Sequence))
        {
            if (auditEvent.Sequence != expectedSequence
                || auditEvent.PreviousHash != expectedPreviousHash
                || auditEvent.Hash != auditEvent.RecalculateHash())
            {
                return AuditChainVerificationResult.Invalid(expectedSequence);
            }

            expectedPreviousHash = auditEvent.Hash;
            expectedSequence++;
        }

        return AuditChainVerificationResult.Valid;
    }
}
