using DecisionForge.Domain.Common;
using DecisionForge.Domain.Decisions;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.PurchaseRequests;
using DecisionForge.Domain.PurchaseRequests.Events;
using DecisionForge.Domain.UnitTests.Builders;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.UnitTests.PurchaseRequests;

public sealed class PurchaseRequestTransitionTests
{
    private static readonly DateTimeOffset _submittedAt =
        PurchaseRequestBuilder.DefaultTime.AddMinutes(1);

    [Fact]
    public void SubmitRequiresAnItemAndLeavesDraftUnchangedOnFailure()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().Build();
        request.ClearDomainEvents();

        AssertInvalidState(() => request.Submit(
            request.ConcurrencyToken,
            PurchaseRequestBuilder.Token(10),
            _submittedAt));

        Assert.Equal(PurchaseRequestStatus.Draft, request.Status);
        Assert.Null(request.SubmittedAt);
        Assert.Empty(request.DomainEvents);
    }

    [Fact]
    public void SubmitCapturesAuthoritativeTotalAndExactEvent()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().WithItem().Build();
        request.ClearDomainEvents();

        request.Submit(
            request.ConcurrencyToken,
            PurchaseRequestBuilder.Token(10),
            _submittedAt);

        Assert.Equal(PurchaseRequestStatus.Submitted, request.Status);
        Assert.Equal(_submittedAt, request.SubmittedAt);
        Assert.Equal(_submittedAt, request.LastModifiedAt);
        PurchaseRequestSubmittedDomainEvent submitted =
            Assert.IsType<PurchaseRequestSubmittedDomainEvent>(Assert.Single(request.DomainEvents));
        Assert.Equal(request.Id, submitted.PurchaseRequestId);
        Assert.Equal(request.Total, submitted.Total);
        Assert.Equal(_submittedAt, submitted.OccurredAt);
    }

    [Fact]
    public void EvaluationFailureRetrySequenceUsesOnlyAllowedStatesAndExactEvents()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().WithItem().Build();
        request.Submit(
            request.ConcurrencyToken,
            PurchaseRequestBuilder.Token(10),
            _submittedAt);
        request.ClearDomainEvents();
        DateTimeOffset evaluatingAt = _submittedAt.AddMinutes(1);
        DateTimeOffset failedAt = evaluatingAt.AddMinutes(1);
        DateTimeOffset retriedAt = failedAt.AddMinutes(1);
        ReasonCode reasonCode = ReasonCode.Parse("EVALUATOR_UNAVAILABLE");

        request.BeginEvaluation(
            DecisionTestData.Context(request),
            request.ConcurrencyToken,
            PurchaseRequestBuilder.Token(11),
            evaluatingAt);
        request.MarkEvaluationFailed(
            reasonCode,
            request.ConcurrencyToken,
            PurchaseRequestBuilder.Token(12),
            failedAt);
        request.RetryEvaluation(
            request.ConcurrencyToken,
            PurchaseRequestBuilder.Token(13),
            retriedAt);

        Assert.Equal(PurchaseRequestStatus.Submitted, request.Status);
        Assert.Collection(
            request.DomainEvents,
            domainEvent =>
            {
                PurchaseRequestEvaluationStartedDomainEvent started =
                    Assert.IsType<PurchaseRequestEvaluationStartedDomainEvent>(domainEvent);
                Assert.Equal(DecisionTestData.PolicyId, started.PolicyId);
                Assert.Equal(DecisionTestData.PolicyVersionId, started.PolicyVersionId);
                Assert.Equal(evaluatingAt, started.OccurredAt);
            },
            domainEvent =>
            {
                PurchaseRequestEvaluationFailedDomainEvent failed =
                    Assert.IsType<PurchaseRequestEvaluationFailedDomainEvent>(domainEvent);
                Assert.Equal(reasonCode, failed.ReasonCode);
                Assert.Equal(failedAt, failed.OccurredAt);
            },
            domainEvent =>
            {
                PurchaseRequestEvaluationRetriedDomainEvent retried =
                    Assert.IsType<PurchaseRequestEvaluationRetriedDomainEvent>(domainEvent);
                Assert.Equal(retriedAt, retried.OccurredAt);
            });
    }

    [Theory]
    [InlineData(DecisionDisposition.AutoApproved, PurchaseRequestStatus.AutoApproved)]
    [InlineData(DecisionDisposition.ManualApprovalRequired, PurchaseRequestStatus.PendingApproval)]
    [InlineData(DecisionDisposition.Rejected, PurchaseRequestStatus.Rejected)]
    public void EvaluationCompletionMapsEveryDispositionToExactRequestState(
        DecisionDisposition disposition,
        PurchaseRequestStatus expectedStatus)
    {
        PurchaseRequest request = new PurchaseRequestBuilder().WithItem().Build();
        request.Submit(
            request.ConcurrencyToken,
            PurchaseRequestBuilder.Token(10),
            _submittedAt);
        PurchaseRequestEvaluationContext context = DecisionTestData.Context(request);
        request.BeginEvaluation(
            context,
            request.ConcurrencyToken,
            PurchaseRequestBuilder.Token(11),
            _submittedAt);
        request.ClearDomainEvents();

        request.CompleteEvaluation(
            disposition,
            request.ConcurrencyToken,
            PurchaseRequestBuilder.Token(12),
            _submittedAt);

        Assert.Equal(expectedStatus, request.Status);
        PurchaseRequestEvaluationCompletedDomainEvent completed =
            Assert.IsType<PurchaseRequestEvaluationCompletedDomainEvent>(
                Assert.Single(request.DomainEvents));
        Assert.Equal(disposition, completed.Disposition);
        Assert.Same(context, request.EvaluationContext);
    }

    [Fact]
    public void RetryRejectsChangedPolicyOrNormalizedInputWithoutStartingEvaluation()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().WithItem().Build();
        request.Submit(
            request.ConcurrencyToken,
            PurchaseRequestBuilder.Token(10),
            _submittedAt);
        PurchaseRequestEvaluationContext original = DecisionTestData.Context(request);
        request.BeginEvaluation(
            original,
            request.ConcurrencyToken,
            PurchaseRequestBuilder.Token(11),
            _submittedAt);
        request.MarkEvaluationFailed(
            ReasonCode.Parse("FAILURE"),
            request.ConcurrencyToken,
            PurchaseRequestBuilder.Token(12),
            _submittedAt);
        request.RetryEvaluation(
            request.ConcurrencyToken,
            PurchaseRequestBuilder.Token(13),
            _submittedAt);
        PurchaseRequestEvaluationContext changed = DecisionTestData.Context(
            request,
            DecisionTestData.PolicySource(
                versionId: Guid.Parse("88888888-8888-4888-8888-888888888889")));
        request.ClearDomainEvents();

        DomainRuleException exception = Assert.Throws<DomainRuleException>(
            () => request.BeginEvaluation(
                changed,
                request.ConcurrencyToken,
                PurchaseRequestBuilder.Token(14),
                _submittedAt));

        Assert.Equal(DecisionErrorCodes.PolicyEvidenceMismatch, exception.Code);
        Assert.Equal(PurchaseRequestStatus.Submitted, request.Status);
        Assert.Same(original, request.EvaluationContext);
        Assert.Empty(request.DomainEvents);
    }

    [Fact]
    public void SubmittedRequestCanBeWithdrawnOnce()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().WithItem().Build();
        request.Submit(
            request.ConcurrencyToken,
            PurchaseRequestBuilder.Token(10),
            _submittedAt);
        request.ClearDomainEvents();
        DateTimeOffset withdrawnAt = _submittedAt.AddMinutes(1);

        request.Withdraw(
            request.ConcurrencyToken,
            PurchaseRequestBuilder.Token(11),
            withdrawnAt);

        Assert.Equal(PurchaseRequestStatus.Withdrawn, request.Status);
        PurchaseRequestWithdrawnDomainEvent withdrawn =
            Assert.IsType<PurchaseRequestWithdrawnDomainEvent>(Assert.Single(request.DomainEvents));
        Assert.Equal(withdrawnAt, withdrawn.OccurredAt);
        AssertInvalidState(() => request.Withdraw(
            request.ConcurrencyToken,
            PurchaseRequestBuilder.Token(12),
            withdrawnAt));
    }

    [Fact]
    public void InvalidTransitionsAreDeniedWithoutAdditionalEvents()
    {
        PurchaseRequest draft = new PurchaseRequestBuilder().WithItem().Build();
        draft.ClearDomainEvents();
        AssertInvalidState(() => draft.BeginEvaluation(
            DecisionTestData.Context(draft),
            draft.ConcurrencyToken,
            PurchaseRequestBuilder.Token(10),
            _submittedAt));
        AssertInvalidState(
            () => draft.MarkEvaluationFailed(
                ReasonCode.Parse("FAILURE"),
                draft.ConcurrencyToken,
                PurchaseRequestBuilder.Token(10),
                _submittedAt));
        AssertInvalidState(() => draft.RetryEvaluation(
            draft.ConcurrencyToken,
            PurchaseRequestBuilder.Token(10),
            _submittedAt));
        AssertInvalidState(() => draft.Withdraw(
            draft.ConcurrencyToken,
            PurchaseRequestBuilder.Token(10),
            _submittedAt));
        Assert.Empty(draft.DomainEvents);

        PurchaseRequest evaluating = new PurchaseRequestBuilder().WithItem().Build();
        evaluating.Submit(
            evaluating.ConcurrencyToken,
            PurchaseRequestBuilder.Token(10),
            _submittedAt);
        evaluating.BeginEvaluation(
            DecisionTestData.Context(evaluating),
            evaluating.ConcurrencyToken,
            PurchaseRequestBuilder.Token(11),
            _submittedAt.AddMinutes(1));
        evaluating.ClearDomainEvents();
        AssertInvalidState(() => evaluating.Submit(
            evaluating.ConcurrencyToken,
            PurchaseRequestBuilder.Token(12),
            _submittedAt.AddMinutes(2)));
        AssertInvalidState(() => evaluating.BeginEvaluation(
            DecisionTestData.Context(evaluating),
            evaluating.ConcurrencyToken,
            PurchaseRequestBuilder.Token(12),
            _submittedAt.AddMinutes(2)));
        AssertInvalidState(() => evaluating.RetryEvaluation(
            evaluating.ConcurrencyToken,
            PurchaseRequestBuilder.Token(12),
            _submittedAt.AddMinutes(2)));
        AssertInvalidState(() => evaluating.Withdraw(
            evaluating.ConcurrencyToken,
            PurchaseRequestBuilder.Token(12),
            _submittedAt.AddMinutes(2)));
        Assert.Empty(evaluating.DomainEvents);
    }

    [Fact]
    public void FailureTransitionRequiresReasonAndUtcMonotonicTime()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().WithItem().Build();
        request.Submit(
            request.ConcurrencyToken,
            PurchaseRequestBuilder.Token(10),
            _submittedAt);
        request.BeginEvaluation(
            DecisionTestData.Context(request),
            request.ConcurrencyToken,
            PurchaseRequestBuilder.Token(11),
            _submittedAt.AddMinutes(1));
        request.ClearDomainEvents();

        Assert.Throws<ArgumentNullException>(
            () => request.MarkEvaluationFailed(
                null!,
                request.ConcurrencyToken,
                PurchaseRequestBuilder.Token(12),
                _submittedAt.AddMinutes(2)));
        Assert.Throws<DomainRuleException>(
            () => request.MarkEvaluationFailed(
                ReasonCode.Parse("FAILURE"),
                request.ConcurrencyToken,
                PurchaseRequestBuilder.Token(12),
                _submittedAt.ToOffset(TimeSpan.FromHours(1))));
        Assert.Equal(PurchaseRequestStatus.Evaluating, request.Status);
        Assert.Empty(request.DomainEvents);
    }

    [Fact]
    public void EarlierTransitionTimeIsRejectedWithoutStateChange()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().WithItem().Build();

        Assert.Throws<DomainRuleException>(
            () => request.Submit(
                request.ConcurrencyToken,
                PurchaseRequestBuilder.Token(10),
                PurchaseRequestBuilder.DefaultTime.AddTicks(-1)));

        Assert.Equal(PurchaseRequestStatus.Draft, request.Status);
        Assert.Null(request.SubmittedAt);
    }

    private static void AssertInvalidState(Action action)
    {
        DomainRuleException exception = Assert.Throws<DomainRuleException>(action);
        Assert.Equal(DomainErrorCodes.InvalidState, exception.Code);
    }
}
