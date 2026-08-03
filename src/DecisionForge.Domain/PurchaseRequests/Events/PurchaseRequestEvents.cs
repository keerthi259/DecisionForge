using DecisionForge.Domain.Approvals;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.PurchaseRequests.Events;

public sealed record PurchaseRequestCreatedDomainEvent(
    Guid PurchaseRequestId,
    Guid RequesterId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PurchaseRequestClonedDomainEvent(
    Guid PurchaseRequestId,
    Guid SourcePurchaseRequestId,
    Guid RequesterId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PurchaseRequestMetadataChangedDomainEvent(
    Guid PurchaseRequestId,
    Guid DepartmentId,
    Guid SupplierId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PurchaseRequestItemAddedDomainEvent(
    Guid PurchaseRequestId,
    Guid ItemId,
    int Quantity,
    Money UnitPrice,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PurchaseRequestItemChangedDomainEvent(
    Guid PurchaseRequestId,
    Guid ItemId,
    int Quantity,
    Money UnitPrice,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PurchaseRequestItemRemovedDomainEvent(
    Guid PurchaseRequestId,
    Guid ItemId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PurchaseRequestSubmittedDomainEvent(
    Guid PurchaseRequestId,
    Money Total,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PurchaseRequestEvaluationStartedDomainEvent(
    Guid PurchaseRequestId,
    Guid PolicyId,
    Guid PolicyVersionId,
    PolicyChecksum PolicyChecksum,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PurchaseRequestEvaluationCompletedDomainEvent(
    Guid PurchaseRequestId,
    DecisionDisposition Disposition,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PurchaseRequestEvaluationFailedDomainEvent(
    Guid PurchaseRequestId,
    ReasonCode ReasonCode,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PurchaseRequestEvaluationRetriedDomainEvent(
    Guid PurchaseRequestId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PurchaseRequestWithdrawnDomainEvent(
    Guid PurchaseRequestId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PurchaseRequestApprovalCompletedDomainEvent(
    Guid PurchaseRequestId,
    Guid ApprovalWorkflowId,
    ApprovalOutcome Outcome,
    DateTimeOffset OccurredAt) : IDomainEvent;
