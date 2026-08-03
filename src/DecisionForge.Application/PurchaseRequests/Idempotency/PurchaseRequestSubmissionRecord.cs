using DecisionForge.Domain.Common;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.PurchaseRequests.Idempotency;

public sealed record PurchaseRequestSubmissionRecord
{
    private PurchaseRequestSubmissionRecord(
        Guid requesterId,
        IdempotencyKey key,
        SubmissionFingerprint fingerprint,
        Guid purchaseRequestId,
        DateTimeOffset completedAt)
    {
        RequesterId = requesterId;
        Key = key;
        Fingerprint = fingerprint;
        PurchaseRequestId = purchaseRequestId;
        CompletedAt = completedAt;
    }

    public Guid RequesterId { get; }

    public IdempotencyKey Key { get; }

    public SubmissionFingerprint Fingerprint { get; }

    public Guid PurchaseRequestId { get; }

    public DateTimeOffset CompletedAt { get; }

    public static PurchaseRequestSubmissionRecord Create(
        Guid requesterId,
        IdempotencyKey key,
        SubmissionFingerprint fingerprint,
        Guid purchaseRequestId,
        DateTimeOffset completedAt)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(fingerprint);
        if (requesterId == Guid.Empty || purchaseRequestId == Guid.Empty)
        {
            throw new DomainRuleException(
                DomainErrorCodes.Validation,
                "Submission idempotency identities must not be empty.");
        }

        if (completedAt.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException(
                DomainErrorCodes.Validation,
                "Submission completion time must use the UTC offset.",
                nameof(completedAt));
        }

        return new PurchaseRequestSubmissionRecord(
            requesterId,
            key,
            fingerprint,
            purchaseRequestId,
            completedAt);
    }
}

public sealed class SubmissionIdempotencyResolution
{
    internal SubmissionIdempotencyResolution(bool isReplay, Guid? originalPurchaseRequestId)
    {
        IsReplay = isReplay;
        OriginalPurchaseRequestId = originalPurchaseRequestId;
    }

    public bool IsReplay { get; }

    public Guid? OriginalPurchaseRequestId { get; }
}

public static class PurchaseRequestSubmissionIdempotency
{
    public static SubmissionIdempotencyResolution Resolve(
        PurchaseRequestSubmissionRecord? existing,
        SubmissionFingerprint requestedFingerprint)
    {
        ArgumentNullException.ThrowIfNull(requestedFingerprint);
        if (existing is null)
        {
            return new SubmissionIdempotencyResolution(false, null);
        }

        if (existing.Fingerprint != requestedFingerprint)
        {
            throw new DomainRuleException(
                PurchaseRequestApplicationErrorCodes.IdempotencyConflict,
                "The idempotency key was already used with different submission input.");
        }

        return new SubmissionIdempotencyResolution(true, existing.PurchaseRequestId);
    }
}
