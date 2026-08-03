using System.Collections.ObjectModel;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Decisions.Events;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.EvaluationFacts;
using DecisionForge.Domain.Policies;
using DecisionForge.Domain.Policies.Evaluation;
using DecisionForge.Domain.Policies.Selection;
using DecisionForge.Domain.PurchaseRequests;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.Decisions;

public sealed class Decision : AggregateRoot
{
    private readonly ReadOnlyCollection<PolicyApproverRole> _requiredApproverRoles;
    private readonly ReadOnlyCollection<DecisionReason> _reasons;
    private readonly ReadOnlyCollection<RuleEvaluation> _rules;

    private Decision(
        Guid id,
        Guid purchaseRequestId,
        PolicyEvaluationSource policy,
        EvaluationFactSnapshot normalizedInput,
        PolicyEvaluationResult result,
        IEnumerable<RuleEvaluation> rules,
        DateTimeOffset decidedAt)
        : base(id)
    {
        PurchaseRequestId = purchaseRequestId;
        PolicyId = policy.PolicyId;
        PolicyVersionId = policy.VersionId;
        PolicyVersionNumber = policy.VersionNumber;
        PolicyChecksum = policy.Checksum;
        NormalizedInput = normalizedInput;
        Disposition = result.Disposition;
        _requiredApproverRoles = new ReadOnlyCollection<PolicyApproverRole>(
            result.RequiredApproverRoles.ToArray());
        _reasons = new ReadOnlyCollection<DecisionReason>(
            result.Reasons.Select(reason => new DecisionReason(reason.Code, reason.Message)).ToArray());
        _rules = new ReadOnlyCollection<RuleEvaluation>(rules.ToArray());
        DefaultOutcomeApplied = result.DefaultOutcomeApplied;
        InputChecksum = result.InputChecksum;
        TraceChecksum = result.TraceChecksum;
        DecidedAt = decidedAt;
    }

    public Guid PurchaseRequestId { get; }

    public Guid PolicyId { get; }

    public Guid PolicyVersionId { get; }

    public PolicyVersionNumber PolicyVersionNumber { get; }

    public PolicyChecksum PolicyChecksum { get; }

    public EvaluationFactSnapshot NormalizedInput { get; }

    public DecisionDisposition Disposition { get; }

    public IReadOnlyList<PolicyApproverRole> RequiredApproverRoles => _requiredApproverRoles;

    public IReadOnlyList<DecisionReason> Reasons => _reasons;

    public IReadOnlyList<RuleEvaluation> Rules => _rules;

    public bool DefaultOutcomeApplied { get; }

    public PolicyChecksum InputChecksum { get; }

    public PolicyChecksum TraceChecksum { get; }

    public DateTimeOffset DecidedAt { get; }

    public void EnsurePolicyMatches(PolicyEvaluationSource policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (PolicyId != policy.PolicyId
            || PolicyVersionId != policy.VersionId
            || PolicyVersionNumber != policy.VersionNumber
            || PolicyChecksum != policy.Checksum)
        {
            throw new DomainRuleException(
                DecisionErrorCodes.PolicyEvidenceMismatch,
                "The policy source does not match the recorded decision evidence.");
        }
    }

    public bool IsEquivalentTo(PolicyEvaluationResult reproduced)
    {
        ArgumentNullException.ThrowIfNull(reproduced);
        return Disposition == reproduced.Disposition
            && DefaultOutcomeApplied == reproduced.DefaultOutcomeApplied
            && InputChecksum == reproduced.InputChecksum
            && TraceChecksum == reproduced.TraceChecksum
            && RequiredApproverRoles.SequenceEqual(reproduced.RequiredApproverRoles)
            && Reasons.Select(reason => (reason.Code, reason.Message)).SequenceEqual(
                reproduced.Reasons.Select(reason => (reason.Code, reason.Message)));
    }

    public static Decision Create(
        Guid id,
        Guid purchaseRequestId,
        PolicyEvaluationSource policy,
        PurchaseRequestEvaluationContext context,
        PolicyEvaluationResult result,
        IReadOnlyList<Guid> ruleEvaluationIds,
        DateTimeOffset decidedAt)
    {
        DomainGuard.NotEmpty(purchaseRequestId, nameof(purchaseRequestId));
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(ruleEvaluationIds);
        context.Policy.EnsureMatches(policy);
        ValidateRuleIds(result, ruleEvaluationIds);
        DateTimeOffset utcDecidedAt = DomainGuard.Utc(decidedAt, nameof(decidedAt));
        RuleEvaluation[] rules = result.Rules
            .Select((evaluation, index) => RuleEvaluation.Create(
                ruleEvaluationIds[index],
                evaluation))
            .ToArray();
        Decision decision = new(
            id,
            purchaseRequestId,
            policy,
            context.NormalizedInput,
            result,
            rules,
            utcDecidedAt);
        decision.Raise(new DecisionRecordedDomainEvent(
            decision.Id,
            purchaseRequestId,
            policy.PolicyId,
            policy.VersionId,
            policy.Checksum,
            result.Disposition,
            utcDecidedAt));
        return decision;
    }

    private static void ValidateRuleIds(
        PolicyEvaluationResult result,
        IReadOnlyCollection<Guid> ruleEvaluationIds)
    {
        if (ruleEvaluationIds.Count != result.Rules.Count
            || ruleEvaluationIds.Any(id => id == Guid.Empty)
            || ruleEvaluationIds.Distinct().Count() != ruleEvaluationIds.Count)
        {
            throw DomainGuard.Validation(
                nameof(ruleEvaluationIds),
                "Rule-evaluation identities must be non-empty, unique and match the trace count.");
        }
    }
}
