using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.Decisions.Events;

public sealed record DecisionRecordedDomainEvent(
    Guid DecisionId,
    Guid PurchaseRequestId,
    Guid PolicyId,
    Guid PolicyVersionId,
    PolicyChecksum PolicyChecksum,
    DecisionDisposition Disposition,
    DateTimeOffset OccurredAt) : IDomainEvent;
