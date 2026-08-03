using DecisionForge.Application.Decisions;
using DecisionForge.Application.PurchaseRequests;
using DecisionForge.Application.UnitTests.PurchaseRequests;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Decisions;
using DecisionForge.Domain.Policies.Selection;
using DecisionForge.Domain.PurchaseRequests;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.UnitTests.Decisions;

public sealed class DecisionEvidenceServiceTests
{
    [Fact]
    public async Task ExplanationReturnsExactPolicyInputAndRuleTraceWithinOwnerScope()
    {
        EvidenceHarness harness = await CreateHarnessAsync();

        DecisionExplanation explanation = await harness.Service.GetExplanationAsync(
            PurchaseRequestApplicationTestData.RequestId,
            CancellationToken.None);

        Assert.Equal(harness.Decision.Id, explanation.DecisionId);
        Assert.Equal(harness.Decision.PolicyId, explanation.PolicyId);
        Assert.Equal(harness.Decision.PolicyVersionId, explanation.PolicyVersionId);
        Assert.Equal(harness.Decision.PolicyChecksum, explanation.PolicyChecksum);
        Assert.Same(harness.Decision.NormalizedInput, explanation.NormalizedInput);
        Assert.Equal(harness.Decision.InputChecksum, explanation.InputChecksum);
        Assert.Equal(harness.Decision.TraceChecksum, explanation.TraceChecksum);
        Assert.Equal(harness.Decision.Rules, explanation.Rules);
        Assert.Equal(
            PurchaseRequestApplicationTestData.RequesterId,
            harness.Repository.RequesterId);
    }

    [Fact]
    public async Task ExplanationDoesNotRevealWhetherAnotherOwnersDecisionExists()
    {
        EvidenceHarness harness = await CreateHarnessAsync();
        harness.CurrentUser.UserId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

        DomainRuleException exception = await Assert.ThrowsAsync<DomainRuleException>(
            () => harness.Service.GetExplanationAsync(
                PurchaseRequestApplicationTestData.RequestId,
                CancellationToken.None));

        Assert.Equal(PurchaseRequestApplicationErrorCodes.NotFound, exception.Code);
    }

    [Fact]
    public async Task HistoricalReproductionUsesExactVersionAndLeavesOriginalUnchanged()
    {
        EvidenceHarness harness = await CreateHarnessAsync();
        int originalEvents = harness.Decision.DomainEvents.Count;

        DecisionReproductionComparison comparison = await harness.Service.ReproduceAsync(
            PurchaseRequestApplicationTestData.RequestId,
            CancellationToken.None);

        Assert.True(comparison.IsEquivalent);
        Assert.Equal(harness.Decision.Id, comparison.DecisionId);
        Assert.Equal(harness.Decision.PolicyVersionId, comparison.PolicyVersionId);
        Assert.Equal(harness.Decision.Disposition, comparison.ReproducedDisposition);
        Assert.Equal(harness.Decision.TraceChecksum, comparison.ReproducedTraceChecksum);
        Assert.Equal(1, harness.PolicyQueries.ExactCalls);
        Assert.Equal(0, harness.PolicyQueries.ListCalls);
        Assert.Equal(originalEvents, harness.Decision.DomainEvents.Count);
        Assert.Same(harness.Decision, harness.Repository.Existing);
    }

    [Fact]
    public async Task ReproductionReportsDriftWithoutChangingOriginalDecision()
    {
        EvidenceHarness harness = await CreateHarnessAsync();
        harness.Engine.ReplacementPolicy = DecisionApplicationTestData.RejectionPolicy();

        DecisionReproductionComparison comparison = await harness.Service.ReproduceAsync(
            PurchaseRequestApplicationTestData.RequestId,
            CancellationToken.None);

        Assert.False(comparison.IsEquivalent);
        Assert.NotEqual(comparison.OriginalDisposition, comparison.ReproducedDisposition);
        Assert.NotEqual(comparison.OriginalTraceChecksum, comparison.ReproducedTraceChecksum);
        Assert.Equal(harness.Decision.TraceChecksum, comparison.OriginalTraceChecksum);
    }

    [Fact]
    public async Task MissingOrMismatchedHistoricalPolicyFailsSafely()
    {
        EvidenceHarness missing = await CreateHarnessAsync();
        missing.PolicyQueries.Exact = null;
        DomainRuleException unavailable = await Assert.ThrowsAsync<DomainRuleException>(
            () => missing.Service.ReproduceAsync(
                PurchaseRequestApplicationTestData.RequestId,
                CancellationToken.None));
        Assert.Equal(DecisionErrorCodes.PolicyEvidenceMismatch, unavailable.Code);

        EvidenceHarness mismatch = await CreateHarnessAsync();
        mismatch.PolicyQueries.Exact = DecisionApplicationTestData.Source(
            policyId: Guid.Parse("77777777-7777-4777-8777-777777777779"),
            versionId: mismatch.Decision.PolicyVersionId);
        DomainRuleException tampered = await Assert.ThrowsAsync<DomainRuleException>(
            () => mismatch.Service.ReproduceAsync(
                PurchaseRequestApplicationTestData.RequestId,
                CancellationToken.None));
        Assert.Equal(DecisionErrorCodes.PolicyEvidenceMismatch, tampered.Code);
    }

    [Fact]
    public async Task ReproductionPropagatesCancellation()
    {
        EvidenceHarness harness = await CreateHarnessAsync();
        using CancellationTokenSource source = new();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => harness.Service.ReproduceAsync(
                PurchaseRequestApplicationTestData.RequestId,
                source.Token));

        Assert.Equal(0, harness.Repository.Calls);
        Assert.Equal(0, harness.Engine.Calls);
    }

    private static async Task<EvidenceHarness> CreateHarnessAsync()
    {
        PurchaseRequest request = PurchaseRequestApplicationTestData.CreateRequest(withItem: true);
        PolicyEvaluationSource policy = DecisionApplicationTestData.Source();
        DecisionServiceHarness submit = new(request, policy);
        DecisionSubmissionResult result = await submit.Service.SubmitAsync(
            new SubmitPurchaseRequestForDecisionCommand(
                request.Id,
                request.ConcurrencyToken,
                IdempotencyKey.Parse("evidence-submit")),
            CancellationToken.None);
        return new EvidenceHarness(result.Decision, policy);
    }

    private sealed class EvidenceHarness
    {
        public EvidenceHarness(Decision decision, PolicyEvaluationSource policy)
        {
            Decision = decision;
            Repository.Existing = decision;
            PolicyQueries.Exact = policy;
            Service = new DecisionEvidenceService(
                Repository,
                PolicyQueries,
                Engine,
                CurrentUser);
        }

        public Decision Decision { get; }

        public RecordingDecisionRepository Repository { get; } = new();

        public StubPolicyDecisionQueries PolicyQueries { get; } = new();

        public StubEvaluationEngine Engine { get; } = new();

        public StubCurrentUser CurrentUser { get; } = new(
            PurchaseRequestApplicationTestData.RequesterId);

        public DecisionEvidenceService Service { get; }
    }
}
