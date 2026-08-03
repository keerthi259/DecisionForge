using DecisionForge.Application.Decisions.Ports;
using DecisionForge.Application.Platform;
using DecisionForge.Application.PurchaseRequests;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Decisions;
using DecisionForge.Domain.Policies.Evaluation;
using DecisionForge.Domain.Policies.Selection;

namespace DecisionForge.Application.Decisions;

public sealed class DecisionEvidenceService
{
    private readonly IDecisionRepository _decisionRepository;
    private readonly IPolicyDecisionQueries _policyQueries;
    private readonly IPolicyEvaluationEngine _evaluationEngine;
    private readonly ICurrentUserContext _currentUser;

    public DecisionEvidenceService(
        IDecisionRepository decisionRepository,
        IPolicyDecisionQueries policyQueries,
        IPolicyEvaluationEngine evaluationEngine,
        ICurrentUserContext currentUser)
    {
        ArgumentNullException.ThrowIfNull(decisionRepository);
        ArgumentNullException.ThrowIfNull(policyQueries);
        ArgumentNullException.ThrowIfNull(evaluationEngine);
        ArgumentNullException.ThrowIfNull(currentUser);
        _decisionRepository = decisionRepository;
        _policyQueries = policyQueries;
        _evaluationEngine = evaluationEngine;
        _currentUser = currentUser;
    }

    public async Task<DecisionExplanation> GetExplanationAsync(
        Guid purchaseRequestId,
        CancellationToken cancellationToken)
    {
        Decision decision = await FindOwnedDecisionAsync(
            purchaseRequestId,
            cancellationToken);
        return new DecisionExplanation(decision);
    }

    public async Task<DecisionReproductionComparison> ReproduceAsync(
        Guid purchaseRequestId,
        CancellationToken cancellationToken)
    {
        Decision decision = await FindOwnedDecisionAsync(
            purchaseRequestId,
            cancellationToken);
        PolicyEvaluationSource? policy = await _policyQueries.FindByVersionIdAsync(
            decision.PolicyVersionId,
            cancellationToken);
        if (policy is null)
        {
            throw new DomainRuleException(
                DecisionErrorCodes.PolicyEvidenceMismatch,
                "The exact policy version recorded by the decision is unavailable.");
        }

        decision.EnsurePolicyMatches(policy);
        PolicyEvaluationResult reproduced = _evaluationEngine.Evaluate(
            policy.Definition,
            PolicyFactSet.FromSnapshot(decision.NormalizedInput),
            cancellationToken);
        return new DecisionReproductionComparison(
            decision.Id,
            decision.PolicyVersionId,
            decision.PolicyChecksum,
            decision.Disposition,
            reproduced.Disposition,
            decision.InputChecksum,
            reproduced.InputChecksum,
            decision.TraceChecksum,
            reproduced.TraceChecksum,
            decision.IsEquivalentTo(reproduced));
    }

    private async Task<Decision> FindOwnedDecisionAsync(
        Guid purchaseRequestId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Guid requesterId = TrustedRequester.RequiredUserId(_currentUser);
        Decision? decision = await _decisionRepository.FindOwnedByPurchaseRequestIdAsync(
            purchaseRequestId,
            requesterId,
            cancellationToken);
        return decision
            ?? throw new DomainRuleException(
                PurchaseRequestApplicationErrorCodes.NotFound,
                $"Purchase request '{purchaseRequestId}' was not found.",
                nameof(purchaseRequestId));
    }
}
