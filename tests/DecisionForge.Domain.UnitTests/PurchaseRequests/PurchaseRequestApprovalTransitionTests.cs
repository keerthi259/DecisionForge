using DecisionForge.Domain.Approvals;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.PurchaseRequests;
using DecisionForge.Domain.PurchaseRequests.Events;
using DecisionForge.Domain.UnitTests.Builders;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.UnitTests.PurchaseRequests;

public sealed class PurchaseRequestApprovalTransitionTests
{
    [Theory]
    [InlineData(ApprovalOutcome.Approved, PurchaseRequestStatus.Approved)]
    [InlineData(ApprovalOutcome.Rejected, PurchaseRequestStatus.Rejected)]
    public void TerminalApprovalOutcomeTransitionsPendingRequest(
        ApprovalOutcome outcome,
        PurchaseRequestStatus expectedStatus)
    {
        PurchaseRequest request = ApprovalWorkflowTestData.PendingRequest();
        ConcurrencyToken originalToken = request.ConcurrencyToken;

        request.CompleteApproval(
            ApprovalWorkflowTestData.WorkflowId,
            outcome,
            originalToken,
            PurchaseRequestBuilder.Token(23),
            PurchaseRequestBuilder.DefaultTime.AddMinutes(1));

        Assert.Equal(expectedStatus, request.Status);
        Assert.NotEqual(originalToken, request.ConcurrencyToken);
        PurchaseRequestApprovalCompletedDomainEvent @event =
            Assert.IsType<PurchaseRequestApprovalCompletedDomainEvent>(Assert.Single(request.DomainEvents));
        Assert.Equal(ApprovalWorkflowTestData.WorkflowId, @event.ApprovalWorkflowId);
        Assert.Equal(outcome, @event.Outcome);
    }

    [Fact]
    public void ApprovalCompletionRejectsWrongStateStaleTokenAndRepeat()
    {
        PurchaseRequest draft = new PurchaseRequestBuilder().WithItem().Build();
        Assert.Throws<DomainRuleException>(() => draft.CompleteApproval(
            ApprovalWorkflowTestData.WorkflowId,
            ApprovalOutcome.Approved,
            draft.ConcurrencyToken,
            PurchaseRequestBuilder.Token(23),
            PurchaseRequestBuilder.DefaultTime));

        PurchaseRequest pending = ApprovalWorkflowTestData.PendingRequest();
        DomainRuleException stale = Assert.Throws<DomainRuleException>(() => pending.CompleteApproval(
            ApprovalWorkflowTestData.WorkflowId,
            ApprovalOutcome.Approved,
            PurchaseRequestBuilder.Token(99),
            PurchaseRequestBuilder.Token(23),
            PurchaseRequestBuilder.DefaultTime));
        Assert.Equal(DomainErrorCodes.ConcurrencyConflict, stale.Code);

        pending.CompleteApproval(
            ApprovalWorkflowTestData.WorkflowId,
            ApprovalOutcome.Approved,
            pending.ConcurrencyToken,
            PurchaseRequestBuilder.Token(23),
            PurchaseRequestBuilder.DefaultTime);
        Assert.Throws<DomainRuleException>(() => pending.CompleteApproval(
            ApprovalWorkflowTestData.WorkflowId,
            ApprovalOutcome.Approved,
            pending.ConcurrencyToken,
            PurchaseRequestBuilder.Token(24),
            PurchaseRequestBuilder.DefaultTime));
    }
}
