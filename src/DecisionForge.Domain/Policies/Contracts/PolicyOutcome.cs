using System.Collections.ObjectModel;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.Policies.Contracts;

public sealed record PolicyOutcome
{
    internal PolicyOutcome(
        DecisionDisposition disposition,
        IEnumerable<PolicyApproverRole> requiredApproverRoles,
        ReasonCode reasonCode,
        string message)
    {
        Disposition = disposition;
        RequiredApproverRoles = new ReadOnlyCollection<PolicyApproverRole>(
            requiredApproverRoles.ToArray());
        ReasonCode = reasonCode;
        Message = message;
    }

    public DecisionDisposition Disposition { get; }

    public IReadOnlyList<PolicyApproverRole> RequiredApproverRoles { get; }

    public ReasonCode ReasonCode { get; }

    public string Message { get; }
}
