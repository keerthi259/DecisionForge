using DecisionForge.Domain.Common;
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

        AssertInvalidState(() => request.Submit(_submittedAt));

        Assert.Equal(PurchaseRequestStatus.Draft, request.Status);
        Assert.Null(request.SubmittedAt);
        Assert.Empty(request.DomainEvents);
    }

    [Fact]
    public void SubmitCapturesAuthoritativeTotalAndExactEvent()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().WithItem().Build();
        request.ClearDomainEvents();

        request.Submit(_submittedAt);

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
        request.Submit(_submittedAt);
        request.ClearDomainEvents();
        DateTimeOffset evaluatingAt = _submittedAt.AddMinutes(1);
        DateTimeOffset failedAt = evaluatingAt.AddMinutes(1);
        DateTimeOffset retriedAt = failedAt.AddMinutes(1);
        ReasonCode reasonCode = ReasonCode.Parse("EVALUATOR_UNAVAILABLE");

        request.BeginEvaluation(evaluatingAt);
        request.MarkEvaluationFailed(reasonCode, failedAt);
        request.RetryEvaluation(retriedAt);

        Assert.Equal(PurchaseRequestStatus.Submitted, request.Status);
        Assert.Collection(
            request.DomainEvents,
            domainEvent =>
            {
                PurchaseRequestEvaluationStartedDomainEvent started =
                    Assert.IsType<PurchaseRequestEvaluationStartedDomainEvent>(domainEvent);
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

    [Fact]
    public void SubmittedRequestCanBeWithdrawnOnce()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().WithItem().Build();
        request.Submit(_submittedAt);
        request.ClearDomainEvents();
        DateTimeOffset withdrawnAt = _submittedAt.AddMinutes(1);

        request.Withdraw(withdrawnAt);

        Assert.Equal(PurchaseRequestStatus.Withdrawn, request.Status);
        PurchaseRequestWithdrawnDomainEvent withdrawn =
            Assert.IsType<PurchaseRequestWithdrawnDomainEvent>(Assert.Single(request.DomainEvents));
        Assert.Equal(withdrawnAt, withdrawn.OccurredAt);
        AssertInvalidState(() => request.Withdraw(withdrawnAt));
    }

    [Fact]
    public void InvalidTransitionsAreDeniedWithoutAdditionalEvents()
    {
        PurchaseRequest draft = new PurchaseRequestBuilder().WithItem().Build();
        draft.ClearDomainEvents();
        AssertInvalidState(() => draft.BeginEvaluation(_submittedAt));
        AssertInvalidState(
            () => draft.MarkEvaluationFailed(ReasonCode.Parse("FAILURE"), _submittedAt));
        AssertInvalidState(() => draft.RetryEvaluation(_submittedAt));
        AssertInvalidState(() => draft.Withdraw(_submittedAt));
        Assert.Empty(draft.DomainEvents);

        PurchaseRequest evaluating = new PurchaseRequestBuilder().WithItem().Build();
        evaluating.Submit(_submittedAt);
        evaluating.BeginEvaluation(_submittedAt.AddMinutes(1));
        evaluating.ClearDomainEvents();
        AssertInvalidState(() => evaluating.Submit(_submittedAt.AddMinutes(2)));
        AssertInvalidState(() => evaluating.BeginEvaluation(_submittedAt.AddMinutes(2)));
        AssertInvalidState(() => evaluating.RetryEvaluation(_submittedAt.AddMinutes(2)));
        AssertInvalidState(() => evaluating.Withdraw(_submittedAt.AddMinutes(2)));
        Assert.Empty(evaluating.DomainEvents);
    }

    [Fact]
    public void FailureTransitionRequiresReasonAndUtcMonotonicTime()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().WithItem().Build();
        request.Submit(_submittedAt);
        request.BeginEvaluation(_submittedAt.AddMinutes(1));
        request.ClearDomainEvents();

        Assert.Throws<ArgumentNullException>(
            () => request.MarkEvaluationFailed(null!, _submittedAt.AddMinutes(2)));
        Assert.Throws<DomainRuleException>(
            () => request.MarkEvaluationFailed(
                ReasonCode.Parse("FAILURE"),
                _submittedAt.ToOffset(TimeSpan.FromHours(1))));
        Assert.Equal(PurchaseRequestStatus.Evaluating, request.Status);
        Assert.Empty(request.DomainEvents);
    }

    [Fact]
    public void EarlierTransitionTimeIsRejectedWithoutStateChange()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().WithItem().Build();

        Assert.Throws<DomainRuleException>(
            () => request.Submit(PurchaseRequestBuilder.DefaultTime.AddTicks(-1)));

        Assert.Equal(PurchaseRequestStatus.Draft, request.Status);
        Assert.Null(request.SubmittedAt);
    }

    private static void AssertInvalidState(Action action)
    {
        DomainRuleException exception = Assert.Throws<DomainRuleException>(action);
        Assert.Equal(DomainErrorCodes.InvalidState, exception.Code);
    }
}
