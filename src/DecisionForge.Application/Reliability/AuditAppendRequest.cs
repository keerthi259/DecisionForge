using DecisionForge.Domain.Audit;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.Reliability;

public sealed record AuditAppendRequest(
    Guid EventId,
    string AggregateType,
    Guid AggregateId,
    string EventType,
    string Actor,
    DateTimeOffset OccurredAt,
    CorrelationId CorrelationId,
    AuditPayload Payload);

public sealed record ReliableEvent(
    AuditAppendRequest Audit,
    DecisionForge.Domain.Outbox.OutboxMessage Outbox);
