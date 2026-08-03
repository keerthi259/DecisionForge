using DecisionForge.Domain.Common;
using DecisionForge.Domain.Decisions;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies.Contracts;
using DecisionForge.Domain.Policies.Serialization;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.Policies.Selection;

public sealed record PolicyEvaluationSource
{
    private PolicyEvaluationSource(
        Guid policyId,
        Guid versionId,
        PolicyVersionNumber versionNumber,
        PolicyStatus status,
        PolicyChecksum checksum,
        PolicyDefinition definition,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveUntil)
    {
        PolicyId = policyId;
        VersionId = versionId;
        VersionNumber = versionNumber;
        Status = status;
        Checksum = checksum;
        Definition = definition;
        EffectiveFrom = effectiveFrom;
        EffectiveUntil = effectiveUntil;
    }

    public Guid PolicyId { get; }

    public Guid VersionId { get; }

    public PolicyVersionNumber VersionNumber { get; }

    public PolicyStatus Status { get; }

    public PolicyChecksum Checksum { get; }

    public PolicyDefinition Definition { get; }

    public DateTimeOffset EffectiveFrom { get; }

    public DateTimeOffset? EffectiveUntil { get; }

    public static PolicyEvaluationSource Create(
        Guid policyId,
        Guid versionId,
        PolicyVersionNumber versionNumber,
        PolicyStatus status,
        PolicyChecksum checksum,
        PolicyDefinition definition,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveUntil)
    {
        DomainGuard.NotEmpty(policyId, nameof(policyId));
        DomainGuard.NotEmpty(versionId, nameof(versionId));
        ArgumentNullException.ThrowIfNull(versionNumber);
        ArgumentNullException.ThrowIfNull(checksum);
        ArgumentNullException.ThrowIfNull(definition);
        if (status == PolicyStatus.Draft)
        {
            throw DomainGuard.Validation(
                nameof(status),
                "A draft policy version cannot be used for decision evaluation.");
        }

        DateTimeOffset utcFrom = DomainGuard.Utc(effectiveFrom, nameof(effectiveFrom));
        DateTimeOffset? utcUntil = effectiveUntil is null
            ? null
            : DomainGuard.Utc(effectiveUntil.Value, nameof(effectiveUntil));
        if (utcUntil is not null && utcUntil <= utcFrom)
        {
            throw DomainGuard.Validation(
                nameof(effectiveUntil),
                "A policy evaluation source requires a non-empty effective range.");
        }

        if (PolicyCanonicalSerializer.CalculateChecksum(definition) != checksum)
        {
            throw new DomainRuleException(
                DecisionErrorCodes.PolicyEvidenceMismatch,
                "The policy definition does not match its published checksum.");
        }

        return new PolicyEvaluationSource(
            policyId,
            versionId,
            versionNumber,
            status,
            checksum,
            definition,
            utcFrom,
            utcUntil);
    }

    public bool IsEffectiveAt(DateTimeOffset timestamp)
    {
        DateTimeOffset utcTimestamp = DomainGuard.Utc(timestamp, nameof(timestamp));
        return Status == PolicyStatus.Published
            && EffectiveFrom <= utcTimestamp
            && (EffectiveUntil is null || utcTimestamp < EffectiveUntil);
    }
}
