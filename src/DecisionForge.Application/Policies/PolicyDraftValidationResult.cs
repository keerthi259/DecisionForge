using System.Collections.ObjectModel;
using DecisionForge.Domain.Policies.Validation;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.Policies;

public sealed record PolicyDraftValidationResult
{
    internal PolicyDraftValidationResult(
        Guid policyId,
        Guid policyVersionId,
        PolicyVersionNumber versionNumber,
        PolicyChecksum? checksum,
        IEnumerable<PolicyValidationError> errors)
    {
        PolicyId = policyId;
        PolicyVersionId = policyVersionId;
        VersionNumber = versionNumber;
        Checksum = checksum;
        Errors = new ReadOnlyCollection<PolicyValidationError>(errors.ToArray());
    }

    public Guid PolicyId { get; }

    public Guid PolicyVersionId { get; }

    public PolicyVersionNumber VersionNumber { get; }

    public PolicyChecksum? Checksum { get; }

    public IReadOnlyList<PolicyValidationError> Errors { get; }

    public bool IsValid => Checksum is not null && Errors.Count == 0;
}
