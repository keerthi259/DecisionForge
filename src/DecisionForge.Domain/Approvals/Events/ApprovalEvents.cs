using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies;

namespace DecisionForge.Domain.Approvals.Events;

public sealed record ApprovalWorkflowCreatedDomainEvent(
    Guid WorkflowId,
    Guid PurchaseRequestId,
    Guid DecisionId,
    IReadOnlyList<PolicyApproverRole> RequiredRoles,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ApprovalStageActivatedDomainEvent(
    Guid WorkflowId,
    Guid StageId,
    PolicyApproverRole RequiredRole,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ApprovalStageApprovedDomainEvent(
    Guid WorkflowId,
    Guid StageId,
    PolicyApproverRole RequiredRole,
    Guid ActorId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ApprovalStageRejectedDomainEvent(
    Guid WorkflowId,
    Guid StageId,
    PolicyApproverRole RequiredRole,
    Guid ActorId,
    string Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ApprovalWorkflowCompletedDomainEvent(
    Guid WorkflowId,
    Guid PurchaseRequestId,
    ApprovalOutcome Outcome,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record DecisionOverrideRecordedDomainEvent(
    Guid WorkflowId,
    Guid PurchaseRequestId,
    Guid DecisionId,
    DecisionDisposition OriginalDisposition,
    ApprovalOutcome Outcome,
    Guid ActorId,
    string Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;
