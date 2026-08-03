using DecisionForge.Domain.Common;
using DecisionForge.Domain.Decisions;
using DecisionForge.Domain.EvaluationFacts;
using DecisionForge.Domain.Policies.Selection;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.PurchaseRequests;

public sealed record EvaluationPolicyReference
{
    private EvaluationPolicyReference(
        Guid policyId,
        Guid versionId,
        PolicyVersionNumber versionNumber,
        PolicyChecksum checksum)
    {
        PolicyId = policyId;
        VersionId = versionId;
        VersionNumber = versionNumber;
        Checksum = checksum;
    }

    public Guid PolicyId { get; }

    public Guid VersionId { get; }

    public PolicyVersionNumber VersionNumber { get; }

    public PolicyChecksum Checksum { get; }

    public static EvaluationPolicyReference Create(PolicyEvaluationSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new EvaluationPolicyReference(
            source.PolicyId,
            source.VersionId,
            source.VersionNumber,
            source.Checksum);
    }

    public void EnsureMatches(PolicyEvaluationSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (PolicyId != source.PolicyId
            || VersionId != source.VersionId
            || VersionNumber != source.VersionNumber
            || Checksum != source.Checksum)
        {
            throw new DomainRuleException(
                DecisionErrorCodes.PolicyEvidenceMismatch,
                "The loaded policy version does not match the original evaluation evidence.");
        }
    }
}

public sealed record PurchaseRequestEvaluationContext
{
    private PurchaseRequestEvaluationContext(
        EvaluationPolicyReference policy,
        EvaluationFactSnapshot normalizedInput)
    {
        Policy = policy;
        NormalizedInput = normalizedInput;
    }

    public EvaluationPolicyReference Policy { get; }

    public EvaluationFactSnapshot NormalizedInput { get; }

    public static PurchaseRequestEvaluationContext Create(
        PolicyEvaluationSource source,
        EvaluationFactSnapshot normalizedInput)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(normalizedInput);
        return new PurchaseRequestEvaluationContext(
            EvaluationPolicyReference.Create(source),
            normalizedInput);
    }
}
