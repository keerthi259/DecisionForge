using DecisionForge.Domain.Enums;

namespace DecisionForge.Domain.Approvals;

public sealed record ApprovalOverride
{
    internal ApprovalOverride(
        DecisionDisposition originalDisposition,
        ApprovalOutcome outcome,
        Guid actorId,
        string reason,
        DateTimeOffset occurredAt)
    {
        OriginalDisposition = originalDisposition;
        Outcome = outcome;
        ActorId = actorId;
        Reason = reason;
        OccurredAt = occurredAt;
    }

    public DecisionDisposition OriginalDisposition { get; }

    public ApprovalOutcome Outcome { get; }

    public Guid ActorId { get; }

    public string Reason { get; }

    public DateTimeOffset OccurredAt { get; }
}
