using System.Collections.ObjectModel;
using DecisionForge.Domain.Approvals.Events;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Decisions;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.Approvals;

public sealed class ApprovalWorkflow : AggregateRoot
{
    public const int MaximumNoteLength = 500;

    private readonly List<ApprovalStage> _stages;
    private readonly ReadOnlyCollection<ApprovalStage> _stagesView;

    private ApprovalWorkflow(
        Guid id,
        Decision decision,
        List<ApprovalStage> stages,
        DateTimeOffset createdAt)
        : base(id)
    {
        PurchaseRequestId = decision.PurchaseRequestId;
        DecisionId = decision.Id;
        OriginalDisposition = decision.Disposition;
        _stages = stages;
        _stagesView = stages.AsReadOnly();
        Status = ApprovalWorkflowStatus.Active;
        CreatedAt = createdAt;
        LastModifiedAt = createdAt;
    }

    public Guid PurchaseRequestId { get; }

    public Guid DecisionId { get; }

    public DecisionDisposition OriginalDisposition { get; }

    public ApprovalWorkflowStatus Status { get; private set; }

    public IReadOnlyList<ApprovalStage> Stages => _stagesView;

    public ApprovalOverride? Override { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset LastModifiedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public ApprovalStage? CurrentStage =>
        _stages.SingleOrDefault(stage => stage.Status == ApprovalStageStatus.Pending);

    public static ApprovalWorkflow Create(
        Guid id,
        Decision decision,
        IReadOnlyList<Guid> stageIds,
        IReadOnlyList<ConcurrencyToken> stageTokens,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(stageIds);
        ArgumentNullException.ThrowIfNull(stageTokens);
        if (decision.Disposition != DecisionDisposition.ManualApprovalRequired)
        {
            throw new DomainRuleException(
                ApprovalErrorCodes.ManualDecisionRequired,
                "An approval workflow can be created only for a manual decision.",
                nameof(decision));
        }

        IReadOnlyList<PolicyApproverRole> plan =
            ApprovalStagePlanBuilder.Build(decision.RequiredApproverRoles);
        ApprovalWorkflowGuard.ValidateStageIdentity(plan.Count, stageIds, stageTokens);
        DateTimeOffset utcCreatedAt = DomainGuard.Utc(createdAt, nameof(createdAt));
        List<ApprovalStage> stages = plan
            .Select((role, index) => new ApprovalStage(
                stageIds[index],
                index + 1,
                role,
                index == 0 ? ApprovalStageStatus.Pending : ApprovalStageStatus.Waiting,
                stageTokens[index]))
            .ToList();
        ApprovalWorkflow workflow = new(id, decision, stages, utcCreatedAt);
        workflow.Raise(new ApprovalWorkflowCreatedDomainEvent(
            workflow.Id,
            workflow.PurchaseRequestId,
            workflow.DecisionId,
            Array.AsReadOnly(plan.ToArray()),
            utcCreatedAt));
        workflow.Raise(new ApprovalStageActivatedDomainEvent(
            workflow.Id,
            stages[0].Id,
            stages[0].RequiredRole,
            utcCreatedAt));
        return workflow;
    }

    public ApprovalStage FindStage(Guid stageId)
    {
        ApprovalStage? stage = _stages.SingleOrDefault(candidate => candidate.Id == stageId);
        return stage ?? throw new DomainRuleException(
            ApprovalErrorCodes.StageNotFound,
            $"Approval stage '{stageId}' was not found.",
            nameof(stageId));
    }

    public void Approve(
        Guid stageId,
        PolicyApproverRole actorRole,
        Guid actorId,
        string? note,
        ConcurrencyToken expectedToken,
        ConcurrencyToken nextStageToken,
        ConcurrencyToken nextActivationToken,
        DateTimeOffset occurredAt)
    {
        EnsureActive();
        ApprovalStage stage = FindStage(stageId);
        EnsureActorRole(stage, actorRole);
        DateTimeOffset utcOccurredAt = ApprovalWorkflowGuard.ActionTime(
            LastModifiedAt,
            occurredAt);
        string? normalizedNote = ApprovalWorkflowGuard.OptionalNote(note);
        int nextIndex = stage.Sequence;
        ApprovalStage? next = nextIndex < _stages.Count ? _stages[nextIndex] : null;
        next?.EnsureActivation(nextActivationToken);
        stage.Approve(actorId, normalizedNote, expectedToken, nextStageToken, utcOccurredAt);
        Raise(new ApprovalStageApprovedDomainEvent(
            Id,
            stage.Id,
            stage.RequiredRole,
            actorId,
            utcOccurredAt));

        if (next is not null)
        {
            next.Activate(nextActivationToken);
            Raise(new ApprovalStageActivatedDomainEvent(
                Id,
                next.Id,
                next.RequiredRole,
                utcOccurredAt));
            LastModifiedAt = utcOccurredAt;
            return;
        }

        Complete(ApprovalOutcome.Approved, utcOccurredAt);
    }

    public void Reject(
        Guid stageId,
        PolicyApproverRole actorRole,
        Guid actorId,
        string reason,
        ConcurrencyToken expectedToken,
        ConcurrencyToken nextStageToken,
        DateTimeOffset occurredAt)
    {
        EnsureActive();
        ApprovalStage stage = FindStage(stageId);
        EnsureActorRole(stage, actorRole);
        string normalizedReason = ApprovalWorkflowGuard.RequiredNote(
            reason,
            ApprovalErrorCodes.RejectionReasonRequired,
            nameof(reason));
        DateTimeOffset utcOccurredAt = ApprovalWorkflowGuard.ActionTime(
            LastModifiedAt,
            occurredAt);
        stage.Reject(actorId, normalizedReason, expectedToken, nextStageToken, utcOccurredAt);
        foreach (ApprovalStage future in _stages.Where(candidate => candidate.Sequence > stage.Sequence))
        {
            future.Skip();
        }

        Raise(new ApprovalStageRejectedDomainEvent(
            Id,
            stage.Id,
            stage.RequiredRole,
            actorId,
            normalizedReason,
            utcOccurredAt));
        Complete(ApprovalOutcome.Rejected, utcOccurredAt);
    }

    public void OverrideDecision(
        ApprovalOutcome outcome,
        Guid actorId,
        string reason,
        ConcurrencyToken expectedCurrentStageToken,
        ConcurrencyToken nextCurrentStageToken,
        DateTimeOffset occurredAt)
    {
        EnsureActive();
        if (!Enum.IsDefined(outcome))
        {
            throw DomainGuard.Validation(nameof(outcome), "The approval override outcome is not supported.");
        }

        string normalizedReason = ApprovalWorkflowGuard.RequiredNote(
            reason,
            ApprovalErrorCodes.OverrideReasonRequired,
            nameof(reason));
        DateTimeOffset utcOccurredAt = ApprovalWorkflowGuard.ActionTime(
            LastModifiedAt,
            occurredAt);
        Guid validatedActorId = DomainGuard.NotEmpty(actorId, nameof(actorId));
        ApprovalStage pending = CurrentStage!;
        pending.CancelPending(expectedCurrentStageToken, nextCurrentStageToken);
        foreach (ApprovalStage waiting in _stages.Where(stage => stage.Status == ApprovalStageStatus.Waiting))
        {
            waiting.Cancel();
        }

        Override = new ApprovalOverride(
            OriginalDisposition,
            outcome,
            validatedActorId,
            normalizedReason,
            utcOccurredAt);
        Raise(new DecisionOverrideRecordedDomainEvent(
            Id,
            PurchaseRequestId,
            DecisionId,
            OriginalDisposition,
            outcome,
            validatedActorId,
            normalizedReason,
            utcOccurredAt));
        Complete(outcome, utcOccurredAt);
    }

    private void Complete(ApprovalOutcome outcome, DateTimeOffset occurredAt)
    {
        Status = outcome == ApprovalOutcome.Approved
            ? ApprovalWorkflowStatus.Approved
            : ApprovalWorkflowStatus.Rejected;
        LastModifiedAt = occurredAt;
        CompletedAt = occurredAt;
        Raise(new ApprovalWorkflowCompletedDomainEvent(
            Id,
            PurchaseRequestId,
            outcome,
            occurredAt));
    }

    private void EnsureActive()
    {
        if (Status != ApprovalWorkflowStatus.Active)
        {
            throw new DomainRuleException(
                ApprovalErrorCodes.NotActionable,
                "The approval workflow is already complete.");
        }
    }

    private static void EnsureActorRole(ApprovalStage stage, PolicyApproverRole actorRole)
    {
        if (!Enum.IsDefined(actorRole) || stage.RequiredRole != actorRole)
        {
            throw new DomainRuleException(
                ApprovalErrorCodes.RoleMismatch,
                "The actor role does not match the pending approval stage.",
                nameof(actorRole));
        }
    }

}
