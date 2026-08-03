using System.Collections.ObjectModel;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies;
using DecisionForge.Domain.Policies.Evaluation;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.Decisions;

public sealed class RuleEvaluation : Entity
{
    private RuleEvaluation(
        Guid id,
        string ruleId,
        int priority,
        PolicyConditionEvaluation condition,
        DecisionDisposition? matchedDisposition,
        IEnumerable<PolicyApproverRole> requiredApproverRoles,
        ReasonCode? reasonCode,
        string? message)
        : base(id)
    {
        RuleId = ruleId;
        Priority = priority;
        Condition = condition;
        MatchedDisposition = matchedDisposition;
        RequiredApproverRoles = new ReadOnlyCollection<PolicyApproverRole>(
            requiredApproverRoles.ToArray());
        ReasonCode = reasonCode;
        Message = message;
    }

    public string RuleId { get; }

    public int Priority { get; }

    public bool Matched => Condition.Result;

    public PolicyConditionEvaluation Condition { get; }

    public DecisionDisposition? MatchedDisposition { get; }

    public IReadOnlyList<PolicyApproverRole> RequiredApproverRoles { get; }

    public ReasonCode? ReasonCode { get; }

    public string? Message { get; }

    internal static RuleEvaluation Create(Guid id, PolicyRuleEvaluation evaluation)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        return new RuleEvaluation(
            id,
            evaluation.RuleId,
            evaluation.Priority,
            evaluation.Condition,
            evaluation.MatchedOutcome?.Disposition,
            evaluation.MatchedOutcome?.RequiredApproverRoles ?? [],
            evaluation.MatchedOutcome?.ReasonCode,
            evaluation.MatchedOutcome?.Message);
    }
}
