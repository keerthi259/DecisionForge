using System.Collections.ObjectModel;
using DecisionForge.Application.Reliability;
using DecisionForge.Domain.Approvals.Events;
using DecisionForge.Domain.Common;

namespace DecisionForge.Application.Approvals.Auditing;

public sealed record ApprovalAuditRecord
{
    internal ApprovalAuditRecord(
        Guid aggregateId,
        string eventType,
        DateTimeOffset occurredAt,
        IDictionary<string, string> fields)
    {
        AggregateId = aggregateId;
        AggregateType = "ApprovalWorkflow";
        EventType = eventType;
        OccurredAt = occurredAt;
        Fields = new ReadOnlyDictionary<string, string>(fields);
    }

    public Guid AggregateId { get; }

    public string AggregateType { get; }

    public string EventType { get; }

    public DateTimeOffset OccurredAt { get; }

    public IReadOnlyDictionary<string, string> Fields { get; }
}

public static class ApprovalAuditMapper
{
    public static ApprovalAuditRecord Map(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return domainEvent switch
        {
            ApprovalWorkflowCreatedDomainEvent created => Record(
                created.WorkflowId,
                "approval-workflow.created",
                created.OccurredAt,
                ("purchaseRequestId", created.PurchaseRequestId.ToString("D")),
                ("decisionId", created.DecisionId.ToString("D")),
                ("requiredRoles", string.Join(',', created.RequiredRoles))),
            ApprovalStageActivatedDomainEvent activated => Record(
                activated.WorkflowId,
                "approval-stage.activated",
                activated.OccurredAt,
                ("stageId", activated.StageId.ToString("D")),
                ("requiredRole", activated.RequiredRole.ToString())),
            ApprovalStageApprovedDomainEvent approved => Record(
                approved.WorkflowId,
                "approval-stage.approved",
                approved.OccurredAt,
                ("stageId", approved.StageId.ToString("D")),
                ("requiredRole", approved.RequiredRole.ToString()),
                ("actorId", approved.ActorId.ToString("D"))),
            ApprovalStageRejectedDomainEvent rejected => Record(
                rejected.WorkflowId,
                "approval-stage.rejected",
                rejected.OccurredAt,
                ("stageId", rejected.StageId.ToString("D")),
                ("requiredRole", rejected.RequiredRole.ToString()),
                ("actorId", rejected.ActorId.ToString("D")),
                NoteEvidence(rejected.Reason)),
            ApprovalWorkflowCompletedDomainEvent completed => Record(
                completed.WorkflowId,
                "approval-workflow.completed",
                completed.OccurredAt,
                ("purchaseRequestId", completed.PurchaseRequestId.ToString("D")),
                ("outcome", completed.Outcome.ToString())),
            DecisionOverrideRecordedDomainEvent overridden => Record(
                overridden.WorkflowId,
                "decision.overridden",
                overridden.OccurredAt,
                ("purchaseRequestId", overridden.PurchaseRequestId.ToString("D")),
                ("decisionId", overridden.DecisionId.ToString("D")),
                ("originalDisposition", overridden.OriginalDisposition.ToString()),
                ("outcome", overridden.Outcome.ToString()),
                ("actorId", overridden.ActorId.ToString("D")),
                NoteEvidence(overridden.Reason)),
            _ => throw new ArgumentException(
                "The domain event is not an approval event.",
                nameof(domainEvent)),
        };
    }

    private static ApprovalAuditRecord Record(
        Guid workflowId,
        string eventType,
        DateTimeOffset occurredAt,
        params (string Name, string Value)[] fields)
    {
        return new ApprovalAuditRecord(
            workflowId,
            eventType,
            occurredAt,
            fields.ToDictionary(field => field.Name, field => field.Value, StringComparer.Ordinal));
    }

    private static (string Name, string Value)[] NoteEvidence(string note)
    {
        return SafeFreeTextEvidence.Create("note", note)
            .Select(field => (field.Key, field.Value))
            .ToArray();
    }

    private static ApprovalAuditRecord Record(
        Guid workflowId,
        string eventType,
        DateTimeOffset occurredAt,
        (string Name, string Value) field1,
        (string Name, string Value) field2,
        (string Name, string Value) field3,
        (string Name, string Value)[] evidence)
    {
        return Record(workflowId, eventType, occurredAt, [field1, field2, field3, .. evidence]);
    }

    private static ApprovalAuditRecord Record(
        Guid workflowId,
        string eventType,
        DateTimeOffset occurredAt,
        (string Name, string Value) field1,
        (string Name, string Value) field2,
        (string Name, string Value) field3,
        (string Name, string Value) field4,
        (string Name, string Value) field5,
        (string Name, string Value)[] evidence)
    {
        return Record(
            workflowId,
            eventType,
            occurredAt,
            [field1, field2, field3, field4, field5, .. evidence]);
    }
}
