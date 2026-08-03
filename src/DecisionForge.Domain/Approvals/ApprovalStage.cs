using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.Approvals;

public sealed class ApprovalStage : Entity
{
    internal ApprovalStage(
        Guid id,
        int sequence,
        PolicyApproverRole requiredRole,
        ApprovalStageStatus status,
        ConcurrencyToken concurrencyToken)
        : base(id)
    {
        Sequence = sequence;
        RequiredRole = requiredRole;
        Status = status;
        ConcurrencyToken = concurrencyToken;
    }

    public int Sequence { get; }

    public PolicyApproverRole RequiredRole { get; }

    public ApprovalStageStatus Status { get; private set; }

    public Guid? ActorId { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset? ActedAt { get; private set; }

    public ConcurrencyToken ConcurrencyToken { get; private set; }

    internal void Approve(
        Guid actorId,
        string? note,
        ConcurrencyToken expectedToken,
        ConcurrencyToken nextToken,
        DateTimeOffset occurredAt)
    {
        DomainGuard.NotEmpty(actorId, nameof(actorId));
        EnsurePending(expectedToken, nextToken);
        Status = ApprovalStageStatus.Approved;
        RecordAction(actorId, note, nextToken, occurredAt);
    }

    internal void Reject(
        Guid actorId,
        string reason,
        ConcurrencyToken expectedToken,
        ConcurrencyToken nextToken,
        DateTimeOffset occurredAt)
    {
        DomainGuard.NotEmpty(actorId, nameof(actorId));
        EnsurePending(expectedToken, nextToken);
        Status = ApprovalStageStatus.Rejected;
        RecordAction(actorId, reason, nextToken, occurredAt);
    }

    internal void Activate(ConcurrencyToken nextToken)
    {
        EnsureActivation(nextToken);
        Rotate(nextToken);
        Status = ApprovalStageStatus.Pending;
    }

    internal void EnsureActivation(ConcurrencyToken nextToken)
    {
        ArgumentNullException.ThrowIfNull(nextToken);
        if (Status != ApprovalStageStatus.Waiting)
        {
            throw NotActionable();
        }

        if (ConcurrencyToken == nextToken)
        {
            throw DomainGuard.Validation(
                nameof(nextToken),
                "Activation must rotate the approval-stage token.");
        }
    }

    internal void Skip()
    {
        if (Status == ApprovalStageStatus.Waiting)
        {
            Status = ApprovalStageStatus.Skipped;
        }
    }

    internal void Cancel(ConcurrencyToken? nextToken = null)
    {
        if (Status is ApprovalStageStatus.Pending or ApprovalStageStatus.Waiting)
        {
            if (nextToken is not null)
            {
                Rotate(nextToken);
            }

            Status = ApprovalStageStatus.Cancelled;
        }
    }

    internal void CancelPending(
        ConcurrencyToken expectedToken,
        ConcurrencyToken nextToken)
    {
        EnsurePending(expectedToken, nextToken);
        Status = ApprovalStageStatus.Cancelled;
    }

    private void EnsurePending(
        ConcurrencyToken expectedToken,
        ConcurrencyToken nextToken)
    {
        ArgumentNullException.ThrowIfNull(expectedToken);
        ArgumentNullException.ThrowIfNull(nextToken);
        if (ConcurrencyToken != expectedToken)
        {
            throw new DomainRuleException(
                DomainErrorCodes.ConcurrencyConflict,
                "The approval stage was changed by another operation.");
        }

        if (Status != ApprovalStageStatus.Pending)
        {
            throw NotActionable();
        }

        Rotate(nextToken);
    }

    private void Rotate(ConcurrencyToken nextToken)
    {
        if (ConcurrencyToken == nextToken)
        {
            throw DomainGuard.Validation(
                nameof(nextToken),
                "The next approval-stage token must differ from the current token.");
        }

        ConcurrencyToken = nextToken;
    }

    private void RecordAction(
        Guid actorId,
        string? note,
        ConcurrencyToken nextToken,
        DateTimeOffset occurredAt)
    {
        ActorId = actorId;
        Note = note;
        ActedAt = occurredAt;
        ConcurrencyToken = nextToken;
    }

    private static DomainRuleException NotActionable()
    {
        return new DomainRuleException(
            ApprovalErrorCodes.NotActionable,
            "Only the currently pending approval stage can be acted on.");
    }
}
