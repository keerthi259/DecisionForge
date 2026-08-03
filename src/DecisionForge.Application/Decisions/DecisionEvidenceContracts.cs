using System.Collections.ObjectModel;
using DecisionForge.Domain.Decisions;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.EvaluationFacts;
using DecisionForge.Domain.Policies;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.Decisions;

public sealed class DecisionExplanation
{
    public DecisionExplanation(Decision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        DecisionId = decision.Id;
        PurchaseRequestId = decision.PurchaseRequestId;
        PolicyId = decision.PolicyId;
        PolicyVersionId = decision.PolicyVersionId;
        PolicyVersionNumber = decision.PolicyVersionNumber;
        PolicyChecksum = decision.PolicyChecksum;
        NormalizedInput = decision.NormalizedInput;
        Disposition = decision.Disposition;
        RequiredApproverRoles = new ReadOnlyCollection<PolicyApproverRole>(
            decision.RequiredApproverRoles.ToArray());
        Reasons = new ReadOnlyCollection<DecisionReason>(decision.Reasons.ToArray());
        Rules = new ReadOnlyCollection<RuleEvaluation>(decision.Rules.ToArray());
        DefaultOutcomeApplied = decision.DefaultOutcomeApplied;
        InputChecksum = decision.InputChecksum;
        TraceChecksum = decision.TraceChecksum;
        DecidedAt = decision.DecidedAt;
    }

    public Guid DecisionId { get; }

    public Guid PurchaseRequestId { get; }

    public Guid PolicyId { get; }

    public Guid PolicyVersionId { get; }

    public PolicyVersionNumber PolicyVersionNumber { get; }

    public PolicyChecksum PolicyChecksum { get; }

    public EvaluationFactSnapshot NormalizedInput { get; }

    public DecisionDisposition Disposition { get; }

    public IReadOnlyList<PolicyApproverRole> RequiredApproverRoles { get; }

    public IReadOnlyList<DecisionReason> Reasons { get; }

    public IReadOnlyList<RuleEvaluation> Rules { get; }

    public bool DefaultOutcomeApplied { get; }

    public PolicyChecksum InputChecksum { get; }

    public PolicyChecksum TraceChecksum { get; }

    public DateTimeOffset DecidedAt { get; }
}

public sealed class DecisionReproductionComparison
{
    public DecisionReproductionComparison(
        Guid decisionId,
        Guid policyVersionId,
        PolicyChecksum policyChecksum,
        DecisionDisposition originalDisposition,
        DecisionDisposition reproducedDisposition,
        PolicyChecksum originalInputChecksum,
        PolicyChecksum reproducedInputChecksum,
        PolicyChecksum originalTraceChecksum,
        PolicyChecksum reproducedTraceChecksum,
        bool isEquivalent)
    {
        DecisionId = decisionId;
        PolicyVersionId = policyVersionId;
        PolicyChecksum = policyChecksum;
        OriginalDisposition = originalDisposition;
        ReproducedDisposition = reproducedDisposition;
        OriginalInputChecksum = originalInputChecksum;
        ReproducedInputChecksum = reproducedInputChecksum;
        OriginalTraceChecksum = originalTraceChecksum;
        ReproducedTraceChecksum = reproducedTraceChecksum;
        IsEquivalent = isEquivalent;
    }

    public Guid DecisionId { get; }

    public Guid PolicyVersionId { get; }

    public PolicyChecksum PolicyChecksum { get; }

    public DecisionDisposition OriginalDisposition { get; }

    public DecisionDisposition ReproducedDisposition { get; }

    public PolicyChecksum OriginalInputChecksum { get; }

    public PolicyChecksum ReproducedInputChecksum { get; }

    public PolicyChecksum OriginalTraceChecksum { get; }

    public PolicyChecksum ReproducedTraceChecksum { get; }

    public bool IsEquivalent { get; }
}
