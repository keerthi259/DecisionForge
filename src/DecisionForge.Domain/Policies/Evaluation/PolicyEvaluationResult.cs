using System.Collections.ObjectModel;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.Policies.Evaluation;

public sealed record PolicyEvaluationReason
{
    internal PolicyEvaluationReason(ReasonCode code, string message)
    {
        Code = code;
        Message = message;
    }

    public ReasonCode Code { get; }

    public string Message { get; }
}

public sealed record PolicyEvaluationResult
{
    internal PolicyEvaluationResult(
        DecisionDisposition disposition,
        IEnumerable<PolicyApproverRole> requiredApproverRoles,
        IEnumerable<PolicyEvaluationReason> reasons,
        IEnumerable<PolicyRuleEvaluation> rules,
        bool defaultOutcomeApplied,
        PolicyChecksum inputChecksum,
        PolicyChecksum traceChecksum)
    {
        Disposition = disposition;
        RequiredApproverRoles = new ReadOnlyCollection<PolicyApproverRole>(
            requiredApproverRoles.ToArray());
        Reasons = new ReadOnlyCollection<PolicyEvaluationReason>(reasons.ToArray());
        Rules = new ReadOnlyCollection<PolicyRuleEvaluation>(rules.ToArray());
        DefaultOutcomeApplied = defaultOutcomeApplied;
        InputChecksum = inputChecksum;
        TraceChecksum = traceChecksum;
    }

    public DecisionDisposition Disposition { get; }

    public IReadOnlyList<PolicyApproverRole> RequiredApproverRoles { get; }

    public IReadOnlyList<PolicyEvaluationReason> Reasons { get; }

    public IReadOnlyList<PolicyRuleEvaluation> Rules { get; }

    public bool DefaultOutcomeApplied { get; }

    public PolicyChecksum InputChecksum { get; }

    public PolicyChecksum TraceChecksum { get; }
}
