using DecisionForge.Domain.Approvals;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Decisions;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.PurchaseRequests.Events;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.PurchaseRequests;

public sealed partial class PurchaseRequest
{
    public void Submit(
        ConcurrencyToken expectedToken,
        ConcurrencyToken nextToken,
        DateTimeOffset occurredAt)
    {
        EnsureStatus(PurchaseRequestStatus.Draft);
        if (_items.Count == 0)
        {
            throw new DomainRuleException(
                DomainErrorCodes.InvalidState,
                "A purchase request requires at least one item before submission.");
        }

        DateTimeOffset utcOccurredAt = ValidateMutation(expectedToken, nextToken, occurredAt);
        Status = PurchaseRequestStatus.Submitted;
        SubmittedAt = utcOccurredAt;
        CompleteMutation(nextToken, utcOccurredAt);
        Raise(new PurchaseRequestSubmittedDomainEvent(Id, Total, utcOccurredAt));
    }

    public void BeginEvaluation(
        PurchaseRequestEvaluationContext evaluationContext,
        ConcurrencyToken expectedToken,
        ConcurrencyToken nextToken,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(evaluationContext);
        EnsureStatus(PurchaseRequestStatus.Submitted);
        DateTimeOffset utcOccurredAt = ValidateMutation(expectedToken, nextToken, occurredAt);
        if (EvaluationContext is not null && EvaluationContext != evaluationContext)
        {
            throw new DomainRuleException(
                DecisionErrorCodes.PolicyEvidenceMismatch,
                "An evaluation retry must use the original policy and normalized input.");
        }

        EvaluationContext ??= evaluationContext;
        Status = PurchaseRequestStatus.Evaluating;
        CompleteMutation(nextToken, utcOccurredAt);
        Raise(new PurchaseRequestEvaluationStartedDomainEvent(
            Id,
            EvaluationContext.Policy.PolicyId,
            EvaluationContext.Policy.VersionId,
            EvaluationContext.Policy.Checksum,
            utcOccurredAt));
    }

    public void CompleteEvaluation(
        DecisionDisposition disposition,
        ConcurrencyToken expectedToken,
        ConcurrencyToken nextToken,
        DateTimeOffset occurredAt)
    {
        EnsureStatus(PurchaseRequestStatus.Evaluating);
        DateTimeOffset utcOccurredAt = ValidateMutation(expectedToken, nextToken, occurredAt);
        Status = disposition switch
        {
            DecisionDisposition.AutoApproved => PurchaseRequestStatus.AutoApproved,
            DecisionDisposition.ManualApprovalRequired => PurchaseRequestStatus.PendingApproval,
            DecisionDisposition.Rejected => PurchaseRequestStatus.Rejected,
            _ => throw DomainGuard.Validation(
                nameof(disposition),
                "The evaluation disposition is not supported."),
        };
        CompleteMutation(nextToken, utcOccurredAt);
        Raise(new PurchaseRequestEvaluationCompletedDomainEvent(
            Id,
            disposition,
            utcOccurredAt));
    }

    public void MarkEvaluationFailed(
        ReasonCode reasonCode,
        ConcurrencyToken expectedToken,
        ConcurrencyToken nextToken,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(reasonCode);
        EnsureStatus(PurchaseRequestStatus.Evaluating);
        DateTimeOffset utcOccurredAt = ValidateMutation(expectedToken, nextToken, occurredAt);
        Status = PurchaseRequestStatus.EvaluationFailed;
        CompleteMutation(nextToken, utcOccurredAt);
        Raise(new PurchaseRequestEvaluationFailedDomainEvent(Id, reasonCode, utcOccurredAt));
    }

    public void RetryEvaluation(
        ConcurrencyToken expectedToken,
        ConcurrencyToken nextToken,
        DateTimeOffset occurredAt)
    {
        EnsureStatus(PurchaseRequestStatus.EvaluationFailed);
        if (EvaluationContext is null)
        {
            throw new DomainRuleException(
                DecisionErrorCodes.EvaluationContextMissing,
                "A failed evaluation has no original policy evidence for retry.");
        }

        DateTimeOffset utcOccurredAt = ValidateMutation(expectedToken, nextToken, occurredAt);
        Status = PurchaseRequestStatus.Submitted;
        CompleteMutation(nextToken, utcOccurredAt);
        Raise(new PurchaseRequestEvaluationRetriedDomainEvent(Id, utcOccurredAt));
    }

    public void Withdraw(
        ConcurrencyToken expectedToken,
        ConcurrencyToken nextToken,
        DateTimeOffset occurredAt)
    {
        EnsureStatus(PurchaseRequestStatus.Submitted, PurchaseRequestStatus.PendingApproval);
        DateTimeOffset utcOccurredAt = ValidateMutation(expectedToken, nextToken, occurredAt);
        Status = PurchaseRequestStatus.Withdrawn;
        CompleteMutation(nextToken, utcOccurredAt);
        Raise(new PurchaseRequestWithdrawnDomainEvent(Id, utcOccurredAt));
    }

    public void CompleteApproval(
        Guid approvalWorkflowId,
        ApprovalOutcome outcome,
        ConcurrencyToken expectedToken,
        ConcurrencyToken nextToken,
        DateTimeOffset occurredAt)
    {
        DomainGuard.NotEmpty(approvalWorkflowId, nameof(approvalWorkflowId));
        EnsureStatus(PurchaseRequestStatus.PendingApproval);
        if (!Enum.IsDefined(outcome))
        {
            throw DomainGuard.Validation(nameof(outcome), "The approval outcome is not supported.");
        }

        DateTimeOffset utcOccurredAt = ValidateMutation(expectedToken, nextToken, occurredAt);
        Status = outcome == ApprovalOutcome.Approved
            ? PurchaseRequestStatus.Approved
            : PurchaseRequestStatus.Rejected;
        CompleteMutation(nextToken, utcOccurredAt);
        Raise(new PurchaseRequestApprovalCompletedDomainEvent(
            Id,
            approvalWorkflowId,
            outcome,
            utcOccurredAt));
    }
}
