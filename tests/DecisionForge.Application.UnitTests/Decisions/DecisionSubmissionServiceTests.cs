using DecisionForge.Application.Decisions;
using DecisionForge.Application.PurchaseRequests;
using DecisionForge.Application.PurchaseRequests.Submission;
using DecisionForge.Application.UnitTests.PurchaseRequests;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Decisions;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies;
using DecisionForge.Domain.Policies.Contracts;
using DecisionForge.Domain.Policies.Selection;
using DecisionForge.Domain.PurchaseRequests;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.UnitTests.Decisions;

public sealed class DecisionSubmissionServiceTests
{
    private static readonly IdempotencyKey _key = IdempotencyKey.Parse("submit-request-001");

    [Fact]
    public async Task FlagshipSubmissionCommitsRequestDecisionAndIdempotencyAtomically()
    {
        PurchaseRequest request = PurchaseRequestApplicationTestData.CreateRequest(withItem: true);
        PolicyEvaluationSource policy = DecisionApplicationTestData.Source();
        DecisionServiceHarness harness = new(request, policy);
        SubmitPurchaseRequestForDecisionCommand command = Command(request);
        using CancellationTokenSource source = new();

        DecisionSubmissionResult result = await harness.Service.SubmitAsync(command, source.Token);

        Assert.False(result.IsReplay);
        Assert.Same(result.Decision, harness.Transaction.Decision);
        Assert.Same(request, harness.Transaction.Request);
        Assert.NotNull(harness.Transaction.ApprovalWorkflow);
        Assert.Equal(result.Decision.Id, harness.Transaction.ApprovalWorkflow!.DecisionId);
        Assert.Equal(
            [PolicyApproverRole.SecurityApprover, PolicyApproverRole.FinanceApprover],
            harness.Transaction.ApprovalWorkflow.Stages.Select(stage => stage.RequiredRole));
        Assert.Single(
            harness.Transaction.ApprovalWorkflow.Stages,
            stage => stage.Status == ApprovalStageStatus.Pending);
        Assert.Equal(PurchaseRequestStatus.PendingApproval, request.Status);
        Assert.Equal(DecisionDisposition.ManualApprovalRequired, result.Decision.Disposition);
        Assert.Equal(
            [PolicyApproverRole.SecurityApprover, PolicyApproverRole.FinanceApprover],
            result.Decision.RequiredApproverRoles);
        Assert.Equal(["HIGH_VALUE", "TECHNOLOGY"], result.Decision.Reasons.Select(x => x.Code.Value));
        Assert.Equal(2, result.Decision.Rules.Count);
        Assert.All(result.Decision.Rules, rule => Assert.True(rule.Matched));
        Assert.Equal(policy.VersionId, request.EvaluationContext!.Policy.VersionId);
        Assert.Same(request.EvaluationContext.NormalizedInput, result.Decision.NormalizedInput);
        Assert.Equal(1, harness.Transaction.DecisionCommits);
        Assert.Equal(0, harness.Transaction.FailureCommits);
        Assert.NotNull(harness.Transaction.IdempotencyRecord);
        Assert.Equal(request.Id, harness.Transaction.IdempotencyRecord!.PurchaseRequestId);
        Assert.Equal(_key, harness.Transaction.IdempotencyRecord.Key);
        Assert.Equal(source.Token, harness.Transaction.LastCancellationToken);
        Assert.Equal(PurchaseRequestApplicationTestData.CurrentTime, harness.PolicyQueries.RequestedTimestamp);
    }

    [Fact]
    public async Task NoEffectivePolicyBlocksSubmissionWithoutChangingDraft()
    {
        PurchaseRequest request = PurchaseRequestApplicationTestData.CreateRequest(withItem: true);
        DecisionServiceHarness harness = new(request, DecisionApplicationTestData.Source());
        harness.PolicyQueries.Candidates = [];
        ConcurrencyToken originalToken = request.ConcurrencyToken;

        DomainRuleException exception = await Assert.ThrowsAsync<DomainRuleException>(
            () => harness.Service.SubmitAsync(Command(request), CancellationToken.None));

        Assert.Equal(DecisionErrorCodes.NoEffectivePolicy, exception.Code);
        Assert.Equal(PurchaseRequestStatus.Draft, request.Status);
        Assert.Equal(originalToken, request.ConcurrencyToken);
        Assert.Null(request.EvaluationContext);
        Assert.Equal(0, harness.Transaction.DecisionCommits);
        Assert.Equal(0, harness.Transaction.FailureCommits);
    }

    [Fact]
    public async Task InvalidReferencesReturnStructuredErrorsBeforePolicySelection()
    {
        PurchaseRequest request = PurchaseRequestApplicationTestData.CreateRequest(withItem: true);
        DecisionServiceHarness harness = new(request, DecisionApplicationTestData.Source());
        harness.Suppliers.Lookup = harness.Suppliers.Lookup! with { IsActive = false };

        SubmissionPreconditionException exception =
            await Assert.ThrowsAsync<SubmissionPreconditionException>(
                () => harness.Service.SubmitAsync(Command(request), CancellationToken.None));

        Assert.Contains(
            exception.Errors,
            error => error.Code == PurchaseRequestApplicationErrorCodes.SupplierInactive);
        Assert.Equal(0, harness.PolicyQueries.ListCalls);
        Assert.Equal(PurchaseRequestStatus.Draft, request.Status);
    }

    [Fact]
    public async Task TechnicalEvaluatorFailurePersistsFailedStateAndNeverCreatesDecision()
    {
        PurchaseRequest request = PurchaseRequestApplicationTestData.CreateRequest(withItem: true);
        StubEvaluationEngine engine = new()
        {
            Failure = new InvalidOperationException("database password must not escape"),
        };
        DecisionServiceHarness harness = new(request, DecisionApplicationTestData.Source(), engine);

        DomainRuleException exception = await Assert.ThrowsAsync<DomainRuleException>(
            () => harness.Service.SubmitAsync(Command(request), CancellationToken.None));

        Assert.Equal(PurchaseRequestApplicationErrorCodes.EvaluationFailed, exception.Code);
        Assert.DoesNotContain("password", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(PurchaseRequestStatus.EvaluationFailed, request.Status);
        Assert.NotNull(request.EvaluationContext);
        Assert.Equal(1, harness.Transaction.FailureCommits);
        Assert.Equal(0, harness.Transaction.DecisionCommits);
        Assert.Null(harness.Transaction.Decision);
        Assert.Null(harness.Transaction.ApprovalWorkflow);
    }

    [Fact]
    public async Task RetryUsesOriginalPolicyAndNormalizedInputInsteadOfCurrentCandidate()
    {
        PurchaseRequest request = PurchaseRequestApplicationTestData.CreateRequest(withItem: true);
        PolicyEvaluationSource original = DecisionApplicationTestData.Source();
        StubEvaluationEngine failing = new() { Failure = new InvalidOperationException("offline") };
        DecisionServiceHarness firstAttempt = new(request, original, failing);
        await Assert.ThrowsAsync<DomainRuleException>(
            () => firstAttempt.Service.SubmitAsync(Command(request), CancellationToken.None));
        PurchaseRequestEvaluationContext originalContext = request.EvaluationContext!;
        PolicyEvaluationSource newer = DecisionApplicationTestData.Source(
            versionId: Guid.Parse("88888888-8888-4888-8888-888888888889"));
        DecisionServiceHarness retry = new(request, original);
        retry.PolicyQueries.Candidates = [newer];
        retry.PolicyQueries.Exact = original;

        DecisionSubmissionResult result = await retry.Service.RetryAsync(
            new RetryPurchaseRequestEvaluationCommand(request.Id, request.ConcurrencyToken),
            CancellationToken.None);

        Assert.Equal(original.VersionId, result.Decision.PolicyVersionId);
        Assert.Same(originalContext, request.EvaluationContext);
        Assert.Same(originalContext.NormalizedInput, result.Decision.NormalizedInput);
        Assert.Equal(0, retry.PolicyQueries.ListCalls);
        Assert.Equal(1, retry.PolicyQueries.ExactCalls);
        Assert.Equal(PurchaseRequestStatus.PendingApproval, request.Status);
        Assert.Null(retry.Transaction.IdempotencyRecord);
    }

    [Fact]
    public async Task MatchingIdempotencyFingerprintReplaysOriginalDecisionWithoutMutation()
    {
        PurchaseRequest request = PurchaseRequestApplicationTestData.CreateRequest(withItem: true);
        DecisionServiceHarness harness = new(request, DecisionApplicationTestData.Source());
        SubmitPurchaseRequestForDecisionCommand command = Command(request);
        DecisionSubmissionResult original = await harness.Service.SubmitAsync(
            command,
            CancellationToken.None);
        harness.IdempotencyStore.Existing = harness.Transaction.IdempotencyRecord;
        harness.DecisionRepository.Existing = original.Decision;
        int idCalls = harness.IdGenerator.Calls;

        DecisionSubmissionResult replay = await harness.Service.SubmitAsync(
            command,
            CancellationToken.None);

        Assert.True(replay.IsReplay);
        Assert.Same(original.Decision, replay.Decision);
        Assert.Equal(1, harness.Transaction.DecisionCommits);
        Assert.Equal(1, harness.RequestRepository.FindCalls);
        Assert.Equal(idCalls, harness.IdGenerator.Calls);
    }

    [Fact]
    public async Task ReusedKeyWithDifferentFingerprintFailsBeforeLoadingRequest()
    {
        PurchaseRequest request = PurchaseRequestApplicationTestData.CreateRequest(withItem: true);
        DecisionServiceHarness harness = new(request, DecisionApplicationTestData.Source());
        SubmitPurchaseRequestForDecisionCommand originalCommand = Command(request);
        DecisionSubmissionResult original = await harness.Service.SubmitAsync(
            originalCommand,
            CancellationToken.None);
        harness.IdempotencyStore.Existing = harness.Transaction.IdempotencyRecord;
        harness.DecisionRepository.Existing = original.Decision;

        DomainRuleException exception = await Assert.ThrowsAsync<DomainRuleException>(
            () => harness.Service.SubmitAsync(
                originalCommand with { ExpectedToken = PurchaseRequestApplicationTestData.Token(99) },
                CancellationToken.None));

        Assert.Equal(PurchaseRequestApplicationErrorCodes.IdempotencyConflict, exception.Code);
        Assert.Equal(1, harness.RequestRepository.FindCalls);
        Assert.Equal(1, harness.Transaction.DecisionCommits);
    }

    [Fact]
    public async Task OwnershipAndCancellationAreEnforcedBeforeMutation()
    {
        PurchaseRequest request = PurchaseRequestApplicationTestData.CreateRequest(withItem: true);
        DecisionServiceHarness denied = new(request, DecisionApplicationTestData.Source());
        denied.CurrentUser.UserId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
        DomainRuleException notFound = await Assert.ThrowsAsync<DomainRuleException>(
            () => denied.Service.SubmitAsync(Command(request), CancellationToken.None));
        Assert.Equal(PurchaseRequestApplicationErrorCodes.NotFound, notFound.Code);

        DecisionServiceHarness cancelled = new(request, DecisionApplicationTestData.Source());
        using CancellationTokenSource source = new();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cancelled.Service.SubmitAsync(Command(request), source.Token));
        Assert.Equal(0, cancelled.IdempotencyStore.FindCalls);
        Assert.Equal(PurchaseRequestStatus.Draft, request.Status);
    }

    [Fact]
    public async Task EvaluationCancellationIsNotTranslatedOrCommittedAsTechnicalFailure()
    {
        PurchaseRequest request = PurchaseRequestApplicationTestData.CreateRequest(withItem: true);
        StubEvaluationEngine engine = new() { Failure = new OperationCanceledException() };
        DecisionServiceHarness harness = new(request, DecisionApplicationTestData.Source(), engine);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => harness.Service.SubmitAsync(Command(request), CancellationToken.None));

        Assert.Equal(0, harness.Transaction.FailureCommits);
        Assert.Equal(0, harness.Transaction.DecisionCommits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NonManualDecisionNeverCreatesApprovalWorkflow(bool rejected)
    {
        PurchaseRequest request = PurchaseRequestApplicationTestData.CreateRequest(withItem: true);
        PolicyDefinition definition = rejected
            ? DecisionApplicationTestData.RejectionPolicy()
            : DecisionApplicationTestData.AutoApprovalPolicy();
        DecisionServiceHarness harness = new(
            request,
            DecisionApplicationTestData.Source(definition));

        DecisionSubmissionResult result = await harness.Service.SubmitAsync(
            Command(request),
            CancellationToken.None);

        Assert.Null(harness.Transaction.ApprovalWorkflow);
        Assert.Equal(
            rejected ? DecisionDisposition.Rejected : DecisionDisposition.AutoApproved,
            result.Decision.Disposition);
    }

    private static SubmitPurchaseRequestForDecisionCommand Command(PurchaseRequest request)
    {
        return new SubmitPurchaseRequestForDecisionCommand(
            request.Id,
            request.ConcurrencyToken,
            _key);
    }
}
