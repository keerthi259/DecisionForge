using DecisionForge.Application.Decisions.Ports;
using DecisionForge.Application.PurchaseRequests.Submission;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Decisions;
using DecisionForge.Domain.Policies.Evaluation;
using DecisionForge.Domain.Policies.Selection;
using DecisionForge.Domain.PurchaseRequests;

namespace DecisionForge.Application.Decisions;

internal sealed class PreparedDecisionEvaluation
{
    public PreparedDecisionEvaluation(
        PolicyEvaluationSource policy,
        PurchaseRequestEvaluationContext context)
    {
        Policy = policy;
        Context = context;
    }

    public PolicyEvaluationSource Policy { get; }

    public PurchaseRequestEvaluationContext Context { get; }
}

public sealed class DecisionEvaluationCoordinator
{
    private readonly PurchaseRequestSubmissionPreconditionValidator _preconditionValidator;
    private readonly IPolicyDecisionQueries _policyQueries;
    private readonly IPolicyEvaluationEngine _evaluationEngine;

    public DecisionEvaluationCoordinator(
        PurchaseRequestSubmissionPreconditionValidator preconditionValidator,
        IPolicyDecisionQueries policyQueries,
        IPolicyEvaluationEngine evaluationEngine)
    {
        ArgumentNullException.ThrowIfNull(preconditionValidator);
        ArgumentNullException.ThrowIfNull(policyQueries);
        ArgumentNullException.ThrowIfNull(evaluationEngine);
        _preconditionValidator = preconditionValidator;
        _policyQueries = policyQueries;
        _evaluationEngine = evaluationEngine;
    }

    internal async Task<PreparedDecisionEvaluation> PrepareInitialAsync(
        PurchaseRequest purchaseRequest,
        DateTimeOffset submissionTimestamp,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SubmissionPreconditionResult validation = await _preconditionValidator.ValidateAsync(
            purchaseRequest,
            cancellationToken);
        if (!validation.IsValid)
        {
            throw new SubmissionPreconditionException(validation.Errors);
        }

        IReadOnlyList<PolicyEvaluationSource> candidates =
            await _policyQueries.ListCandidatesAtAsync(
                submissionTimestamp,
                cancellationToken);
        PolicyEvaluationSource policy = EffectivePolicySelector.Select(
            candidates,
            submissionTimestamp);
        DateOnly evaluationDate = DateOnly.FromDateTime(submissionTimestamp.UtcDateTime);
        PurchaseRequestEvaluationContext context = PurchaseRequestEvaluationContext.Create(
            policy,
            NormalizedEvaluationInputBuilder.Build(purchaseRequest, validation, evaluationDate));
        return new PreparedDecisionEvaluation(policy, context);
    }

    internal async Task<PreparedDecisionEvaluation> PrepareRetryAsync(
        PurchaseRequest purchaseRequest,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PurchaseRequestEvaluationContext context = purchaseRequest.EvaluationContext
            ?? throw new DomainRuleException(
                DecisionErrorCodes.EvaluationContextMissing,
                "The failed request has no original evaluation evidence.");
        PolicyEvaluationSource? policy = await _policyQueries.FindByVersionIdAsync(
            context.Policy.VersionId,
            cancellationToken);
        if (policy is null)
        {
            throw new DomainRuleException(
                DecisionErrorCodes.PolicyEvidenceMismatch,
                "The original policy version is unavailable for evaluation retry.");
        }

        context.Policy.EnsureMatches(policy);
        return new PreparedDecisionEvaluation(policy, context);
    }

    internal PolicyEvaluationResult Evaluate(
        PreparedDecisionEvaluation prepared,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        return _evaluationEngine.Evaluate(
            prepared.Policy.Definition,
            PolicyFactSet.FromSnapshot(prepared.Context.NormalizedInput),
            cancellationToken);
    }
}
