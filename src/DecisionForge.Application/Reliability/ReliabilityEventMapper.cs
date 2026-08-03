using System.Globalization;
using DecisionForge.Application.Platform;
using DecisionForge.Domain.Approvals.Events;
using DecisionForge.Domain.Audit;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Decisions.Events;
using DecisionForge.Domain.Outbox;
using DecisionForge.Domain.Policies.Lifecycle.Events;
using DecisionForge.Domain.PurchaseRequests.Events;
using DecisionForge.Domain.ReferenceData.Events;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.Reliability;

public sealed class ReliabilityEventMapper(IIdGenerator idGenerator)
{
    public ReliableEvent Map(
        IDomainEvent domainEvent,
        string actor,
        CorrelationId correlationId,
        int maximumOutboxAttempts = 5)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        MappedEvent mapped = MapEvent(domainEvent);
        AuditPayload auditPayload = AuditPayload.Create(mapped.Fields);
        Guid auditEventId = idGenerator.Create();
        AuditAppendRequest audit = new(
            auditEventId,
            mapped.AggregateType,
            mapped.AggregateId,
            mapped.EventType,
            actor,
            domainEvent.OccurredAt,
            correlationId,
            auditPayload);
        Dictionary<string, string> outboxFields = new(mapped.Fields, StringComparer.Ordinal)
        {
            ["aggregateType"] = mapped.AggregateType,
            ["aggregateId"] = mapped.AggregateId.ToString("D"),
            ["eventType"] = mapped.EventType,
            ["auditEventId"] = auditEventId.ToString("D"),
        };
        OutboxMessage outbox = OutboxMessage.Create(
            idGenerator.Create(),
            "decisionforge.domain-event.v1",
            AuditPayload.Create(outboxFields),
            domainEvent.OccurredAt,
            domainEvent.OccurredAt,
            maximumOutboxAttempts);
        return new ReliableEvent(audit, outbox);
    }

    private static MappedEvent MapEvent(IDomainEvent domainEvent)
    {
        return domainEvent switch
        {
            PurchaseRequestCreatedDomainEvent value => Event(
                "PurchaseRequest", value.PurchaseRequestId, "purchase-request.created",
                ("requesterId", Id(value.RequesterId))),
            PurchaseRequestClonedDomainEvent value => Event(
                "PurchaseRequest", value.PurchaseRequestId, "purchase-request.cloned",
                ("sourcePurchaseRequestId", Id(value.SourcePurchaseRequestId)),
                ("requesterId", Id(value.RequesterId))),
            PurchaseRequestMetadataChangedDomainEvent value => Event(
                "PurchaseRequest", value.PurchaseRequestId, "purchase-request.metadata-changed",
                ("departmentId", Id(value.DepartmentId)), ("supplierId", Id(value.SupplierId))),
            PurchaseRequestItemAddedDomainEvent value => RequestItem(
                value.PurchaseRequestId, value.ItemId, value.Quantity,
                value.UnitPrice.Amount, value.UnitPrice.Currency.Value, "purchase-request.item-added"),
            PurchaseRequestItemChangedDomainEvent value => RequestItem(
                value.PurchaseRequestId, value.ItemId, value.Quantity,
                value.UnitPrice.Amount, value.UnitPrice.Currency.Value, "purchase-request.item-changed"),
            PurchaseRequestItemRemovedDomainEvent value => Event(
                "PurchaseRequest", value.PurchaseRequestId, "purchase-request.item-removed",
                ("itemId", Id(value.ItemId))),
            PurchaseRequestSubmittedDomainEvent value => Event(
                "PurchaseRequest", value.PurchaseRequestId, "purchase-request.submitted",
                ("totalAmount", Decimal(value.Total.Amount)), ("currency", value.Total.Currency.Value)),
            PurchaseRequestEvaluationStartedDomainEvent value => Event(
                "PurchaseRequest", value.PurchaseRequestId, "purchase-request.evaluation-started",
                ("policyId", Id(value.PolicyId)), ("policyVersionId", Id(value.PolicyVersionId)),
                ("policyChecksum", value.PolicyChecksum.Value)),
            PurchaseRequestEvaluationCompletedDomainEvent value => Event(
                "PurchaseRequest", value.PurchaseRequestId, "purchase-request.evaluation-completed",
                ("disposition", value.Disposition.ToString())),
            PurchaseRequestEvaluationFailedDomainEvent value => Event(
                "PurchaseRequest", value.PurchaseRequestId, "purchase-request.evaluation-failed",
                ("reasonCode", value.ReasonCode.Value)),
            PurchaseRequestEvaluationRetriedDomainEvent value => Event(
                "PurchaseRequest", value.PurchaseRequestId, "purchase-request.evaluation-retried"),
            PurchaseRequestWithdrawnDomainEvent value => Event(
                "PurchaseRequest", value.PurchaseRequestId, "purchase-request.withdrawn"),
            PurchaseRequestApprovalCompletedDomainEvent value => Event(
                "PurchaseRequest", value.PurchaseRequestId, "purchase-request.approval-completed",
                ("approvalWorkflowId", Id(value.ApprovalWorkflowId)),
                ("outcome", value.Outcome.ToString())),
            DecisionRecordedDomainEvent value => Event(
                "Decision", value.DecisionId, "decision.recorded",
                ("purchaseRequestId", Id(value.PurchaseRequestId)), ("policyId", Id(value.PolicyId)),
                ("policyVersionId", Id(value.PolicyVersionId)),
                ("policyChecksum", value.PolicyChecksum.Value),
                ("disposition", value.Disposition.ToString())),
            ApprovalWorkflowCreatedDomainEvent value => Event(
                "ApprovalWorkflow", value.WorkflowId, "approval-workflow.created",
                ("purchaseRequestId", Id(value.PurchaseRequestId)),
                ("decisionId", Id(value.DecisionId)),
                ("requiredRoles", string.Join(',', value.RequiredRoles))),
            ApprovalStageActivatedDomainEvent value => Event(
                "ApprovalWorkflow", value.WorkflowId, "approval-stage.activated",
                ("stageId", Id(value.StageId)), ("requiredRole", value.RequiredRole.ToString())),
            ApprovalStageApprovedDomainEvent value => Event(
                "ApprovalWorkflow", value.WorkflowId, "approval-stage.approved",
                ("stageId", Id(value.StageId)), ("requiredRole", value.RequiredRole.ToString()),
                ("actorId", Id(value.ActorId))),
            ApprovalStageRejectedDomainEvent value => ApprovalWithEvidence(
                value.WorkflowId, "approval-stage.rejected", value.Reason,
                ("stageId", Id(value.StageId)), ("requiredRole", value.RequiredRole.ToString()),
                ("actorId", Id(value.ActorId))),
            ApprovalWorkflowCompletedDomainEvent value => Event(
                "ApprovalWorkflow", value.WorkflowId, "approval-workflow.completed",
                ("purchaseRequestId", Id(value.PurchaseRequestId)),
                ("outcome", value.Outcome.ToString())),
            DecisionOverrideRecordedDomainEvent value => ApprovalWithEvidence(
                value.WorkflowId, "decision.overridden", value.Reason,
                ("purchaseRequestId", Id(value.PurchaseRequestId)),
                ("decisionId", Id(value.DecisionId)),
                ("originalDisposition", value.OriginalDisposition.ToString()),
                ("outcome", value.Outcome.ToString()), ("actorId", Id(value.ActorId))),
            PolicyCreatedDomainEvent value => Event(
                "Policy", value.PolicyId, "policy.created", ("code", value.Code.Value)),
            PolicyVersionDraftCreatedDomainEvent value => PolicyVersion(
                value.PolicyId, value.PolicyVersionId, value.VersionNumber.Value, value.IsValid,
                value.Checksum?.Value, "policy-version.draft-created"),
            PolicyVersionDraftUpdatedDomainEvent value => PolicyVersion(
                value.PolicyId, value.PolicyVersionId, value.VersionNumber.Value, value.IsValid,
                value.Checksum?.Value, "policy-version.draft-updated"),
            PolicyVersionPublishedDomainEvent value => PublishedPolicy(value),
            PolicyVersionRetiredDomainEvent value => RetiredPolicy(value),
            DepartmentCreatedDomainEvent value => Event(
                "Department", value.DepartmentId, "department.created", ("code", value.Code.Value)),
            DepartmentDetailsChangedDomainEvent value => Event(
                "Department", value.DepartmentId, "department.details-changed",
                ("autoApprovalAmount", Decimal(value.AutoApprovalLimit.Amount)),
                ("currency", value.AutoApprovalLimit.Currency.Value)),
            DepartmentActivationChangedDomainEvent value => Event(
                "Department", value.DepartmentId, "department.activation-changed",
                ("isActive", Boolean(value.IsActive))),
            SupplierCreatedDomainEvent value => Event(
                "Supplier", value.SupplierId, "supplier.created",
                ("registrationNumber", value.RegistrationNumber.Value)),
            SupplierDetailsChangedDomainEvent value => Event(
                "Supplier", value.SupplierId, "supplier.details-changed",
                ("approvalStatus", value.ApprovalStatus.ToString()),
                ("onboardingStatus", value.OnboardingStatus.ToString()),
                ("riskRating", value.RiskRating.ToString())),
            SupplierActivationChangedDomainEvent value => Event(
                "Supplier", value.SupplierId, "supplier.activation-changed",
                ("isActive", Boolean(value.IsActive))),
            _ => throw new ArgumentException("The domain event has no reliability mapping.", nameof(domainEvent)),
        };
    }

    private static MappedEvent RequestItem(
        Guid requestId,
        Guid itemId,
        int quantity,
        decimal amount,
        string currency,
        string eventType)
    {
        return Event(
            "PurchaseRequest", requestId, eventType, ("itemId", Id(itemId)),
            ("quantity", quantity.ToString(CultureInfo.InvariantCulture)),
            ("unitPrice", Decimal(amount)), ("currency", currency));
    }

    private static MappedEvent ApprovalWithEvidence(
        Guid workflowId,
        string eventType,
        string reason,
        params (string Name, string Value)[] fields)
    {
        Dictionary<string, string> values = Fields(fields);
        foreach (KeyValuePair<string, string> evidence in SafeFreeTextEvidence.Create("note", reason))
        {
            values.Add(evidence.Key, evidence.Value);
        }

        return new MappedEvent("ApprovalWorkflow", workflowId, eventType, values);
    }

    private static MappedEvent PolicyVersion(
        Guid policyId,
        Guid versionId,
        int version,
        bool isValid,
        string? checksum,
        string eventType)
    {
        Dictionary<string, string> fields = Fields(
            ("policyVersionId", Id(versionId)),
            ("version", version.ToString(CultureInfo.InvariantCulture)),
            ("isValid", Boolean(isValid)));
        if (checksum is not null)
        {
            fields.Add("checksum", checksum);
        }

        return new MappedEvent("Policy", policyId, eventType, fields);
    }

    private static MappedEvent PublishedPolicy(PolicyVersionPublishedDomainEvent value)
    {
        MappedEvent mapped = PolicyVersion(
            value.PolicyId, value.PolicyVersionId, value.VersionNumber.Value, true,
            value.Checksum.Value, "policy-version.published");
        mapped.Fields.Add("effectiveFrom", Time(value.EffectiveFrom));
        if (value.EffectiveUntil is { } until)
        {
            mapped.Fields.Add("effectiveUntil", Time(until));
        }

        return mapped;
    }

    private static MappedEvent RetiredPolicy(PolicyVersionRetiredDomainEvent value)
    {
        MappedEvent mapped = PolicyVersion(
            value.PolicyId, value.PolicyVersionId, value.VersionNumber.Value, true,
            null, "policy-version.retired");
        mapped.Fields.Remove("isValid");
        mapped.Fields.Add("effectiveFrom", Time(value.EffectiveFrom));
        if (value.EffectiveUntil is { } until)
        {
            mapped.Fields.Add("effectiveUntil", Time(until));
        }

        return mapped;
    }

    private static MappedEvent Event(
        string aggregateType,
        Guid aggregateId,
        string eventType,
        params (string Name, string Value)[] fields)
    {
        return new MappedEvent(aggregateType, aggregateId, eventType, Fields(fields));
    }

    private static Dictionary<string, string> Fields(params (string Name, string Value)[] fields)
    {
        return fields.ToDictionary(field => field.Name, field => field.Value, StringComparer.Ordinal);
    }

    private static string Id(Guid value)
    {
        return value.ToString("D");
    }

    private static string Decimal(decimal value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string Boolean(bool value)
    {
        return value ? "true" : "false";
    }

    private static string Time(DateTimeOffset value)
    {
        return value.ToString("O", CultureInfo.InvariantCulture);
    }

    private sealed record MappedEvent(
        string AggregateType,
        Guid AggregateId,
        string EventType,
        Dictionary<string, string> Fields);
}
