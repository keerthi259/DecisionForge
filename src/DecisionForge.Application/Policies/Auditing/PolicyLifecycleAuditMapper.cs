using System.Collections.ObjectModel;
using System.Globalization;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Policies.Lifecycle.Events;

namespace DecisionForge.Application.Policies.Auditing;

public sealed record PolicyLifecycleAuditRecord
{
    internal PolicyLifecycleAuditRecord(
        Guid aggregateId,
        string eventType,
        DateTimeOffset occurredAt,
        IDictionary<string, string> fields)
    {
        AggregateId = aggregateId;
        AggregateType = "Policy";
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

public static class PolicyLifecycleAuditMapper
{
    public static PolicyLifecycleAuditRecord Map(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return domainEvent switch
        {
            PolicyCreatedDomainEvent created => Record(
                created.PolicyId,
                "policy.created",
                created.OccurredAt,
                ("code", created.Code.Value)),
            PolicyVersionDraftCreatedDomainEvent created => Record(
                created.PolicyId,
                "policy-version.draft-created",
                created.OccurredAt,
                VersionFields(
                    created.PolicyVersionId,
                    created.VersionNumber.Value,
                    created.IsValid,
                    created.Checksum?.Value)),
            PolicyVersionDraftUpdatedDomainEvent updated => Record(
                updated.PolicyId,
                "policy-version.draft-updated",
                updated.OccurredAt,
                VersionFields(
                    updated.PolicyVersionId,
                    updated.VersionNumber.Value,
                    updated.IsValid,
                    updated.Checksum?.Value)),
            PolicyVersionPublishedDomainEvent published => Record(
                published.PolicyId,
                "policy-version.published",
                published.OccurredAt,
                PublishedFields(published)),
            PolicyVersionRetiredDomainEvent retired => Record(
                retired.PolicyId,
                "policy-version.retired",
                retired.OccurredAt,
                RetiredFields(retired)),
            _ => throw new ArgumentException(
                "The domain event is not a policy lifecycle event.",
                nameof(domainEvent)),
        };
    }

    private static PolicyLifecycleAuditRecord Record(
        Guid policyId,
        string eventType,
        DateTimeOffset occurredAt,
        params (string Name, string Value)[] fields)
    {
        return Record(
            policyId,
            eventType,
            occurredAt,
            fields.ToDictionary(field => field.Name, field => field.Value, StringComparer.Ordinal));
    }

    private static PolicyLifecycleAuditRecord Record(
        Guid policyId,
        string eventType,
        DateTimeOffset occurredAt,
        Dictionary<string, string> fields)
    {
        return new PolicyLifecycleAuditRecord(policyId, eventType, occurredAt, fields);
    }

    private static Dictionary<string, string> VersionFields(
        Guid versionId,
        int versionNumber,
        bool isValid,
        string? checksum)
    {
        Dictionary<string, string> fields = new(StringComparer.Ordinal)
        {
            ["policyVersionId"] = versionId.ToString("D"),
            ["version"] = versionNumber.ToString(CultureInfo.InvariantCulture),
            ["isValid"] = isValid ? "true" : "false",
        };
        if (checksum is not null)
        {
            fields["checksum"] = checksum;
        }

        return fields;
    }

    private static Dictionary<string, string> PublishedFields(
        PolicyVersionPublishedDomainEvent published)
    {
        Dictionary<string, string> fields = VersionFields(
            published.PolicyVersionId,
            published.VersionNumber.Value,
            isValid: true,
            published.Checksum.Value);
        fields["effectiveFrom"] = published.EffectiveFrom.ToString("O", CultureInfo.InvariantCulture);
        if (published.EffectiveUntil is not null)
        {
            fields["effectiveUntil"] = published.EffectiveUntil.Value.ToString(
                "O",
                CultureInfo.InvariantCulture);
        }

        return fields;
    }

    private static Dictionary<string, string> RetiredFields(
        PolicyVersionRetiredDomainEvent retired)
    {
        Dictionary<string, string> fields = VersionFields(
            retired.PolicyVersionId,
            retired.VersionNumber.Value,
            isValid: true,
            checksum: null);
        fields.Remove("isValid");
        fields["effectiveFrom"] = retired.EffectiveFrom.ToString("O", CultureInfo.InvariantCulture);
        if (retired.EffectiveUntil is not null)
        {
            fields["effectiveUntil"] = retired.EffectiveUntil.Value.ToString(
                "O",
                CultureInfo.InvariantCulture);
        }

        return fields;
    }
}
