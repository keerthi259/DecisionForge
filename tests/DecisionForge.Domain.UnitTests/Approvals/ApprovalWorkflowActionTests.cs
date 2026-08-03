using DecisionForge.Domain.Approvals;
using DecisionForge.Domain.Approvals.Events;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies;
using DecisionForge.Domain.UnitTests.Builders;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.UnitTests.Approvals;

public sealed class ApprovalWorkflowActionTests
{
    [Fact]
    public void ApprovalRotatesActedAndActivatedTokensAndProgressesOneStage()
    {
        ApprovalWorkflow workflow = ApprovalWorkflowTestData.Workflow();
        ApprovalStage first = workflow.Stages[0];
        ApprovalStage second = workflow.Stages[1];
        ConcurrencyToken originalSecondToken = second.ConcurrencyToken;
        workflow.ClearDomainEvents();

        workflow.Approve(
            first.Id,
            first.RequiredRole,
            ApprovalWorkflowTestData.ActorId,
            "  reviewed  ",
            first.ConcurrencyToken,
            ApprovalWorkflowTestData.Token(20),
            ApprovalWorkflowTestData.Token(21),
            PurchaseRequestBuilder.DefaultTime.AddMinutes(1));

        Assert.Equal(ApprovalStageStatus.Approved, first.Status);
        Assert.Equal("reviewed", first.Note);
        Assert.Equal(ApprovalWorkflowTestData.ActorId, first.ActorId);
        Assert.Equal(ApprovalWorkflowTestData.Token(20), first.ConcurrencyToken);
        Assert.Equal(ApprovalStageStatus.Pending, second.Status);
        Assert.NotEqual(originalSecondToken, second.ConcurrencyToken);
        Assert.Equal(ApprovalWorkflowTestData.Token(21), second.ConcurrencyToken);
        Assert.Equal(ApprovalWorkflowStatus.Active, workflow.Status);
        Assert.Single(workflow.Stages, stage => stage.Status == ApprovalStageStatus.Pending);
        Assert.Collection(
            workflow.DomainEvents,
            @event => Assert.IsType<ApprovalStageApprovedDomainEvent>(@event),
            @event => Assert.IsType<ApprovalStageActivatedDomainEvent>(@event));
    }

    [Fact]
    public void FinalApprovalCompletesWorkflowAndRecordsTerminalEvidence()
    {
        ApprovalWorkflow workflow = ApprovalWorkflowTestData.Workflow(
            PolicyApproverRole.FinanceApprover);
        ApprovalStage stage = workflow.CurrentStage!;
        workflow.ClearDomainEvents();
        DateTimeOffset actionTime = PurchaseRequestBuilder.DefaultTime.AddMinutes(2);

        workflow.Approve(
            stage.Id,
            stage.RequiredRole,
            ApprovalWorkflowTestData.ActorId,
            null,
            stage.ConcurrencyToken,
            ApprovalWorkflowTestData.Token(20),
            ApprovalWorkflowTestData.Token(21),
            actionTime);

        Assert.Equal(ApprovalWorkflowStatus.Approved, workflow.Status);
        Assert.Equal(actionTime, workflow.CompletedAt);
        Assert.Null(workflow.CurrentStage);
        ApprovalWorkflowCompletedDomainEvent completed =
            Assert.IsType<ApprovalWorkflowCompletedDomainEvent>(workflow.DomainEvents[^1]);
        Assert.Equal(ApprovalOutcome.Approved, completed.Outcome);
    }

    [Fact]
    public void RejectionRequiresReasonAndTerminatesAllFutureStages()
    {
        ApprovalWorkflow workflow = ApprovalWorkflowTestData.Workflow();
        ApprovalStage stage = workflow.CurrentStage!;
        ConcurrencyToken originalToken = stage.ConcurrencyToken;

        DomainRuleException missing = Assert.Throws<DomainRuleException>(() => workflow.Reject(
            stage.Id,
            stage.RequiredRole,
            ApprovalWorkflowTestData.ActorId,
            "   ",
            originalToken,
            ApprovalWorkflowTestData.Token(20),
            PurchaseRequestBuilder.DefaultTime));
        Assert.Equal(ApprovalErrorCodes.RejectionReasonRequired, missing.Code);
        Assert.Equal(ApprovalStageStatus.Pending, stage.Status);

        workflow.ClearDomainEvents();
        workflow.Reject(
            stage.Id,
            stage.RequiredRole,
            ApprovalWorkflowTestData.ActorId,
            "  Budget evidence is insufficient.  ",
            originalToken,
            ApprovalWorkflowTestData.Token(20),
            PurchaseRequestBuilder.DefaultTime.AddMinutes(1));

        Assert.Equal(ApprovalWorkflowStatus.Rejected, workflow.Status);
        Assert.Equal(ApprovalStageStatus.Rejected, stage.Status);
        Assert.Equal("Budget evidence is insufficient.", stage.Note);
        Assert.All(workflow.Stages.Skip(1), future => Assert.Equal(ApprovalStageStatus.Skipped, future.Status));
        Assert.Null(workflow.CurrentStage);
        Assert.Collection(
            workflow.DomainEvents,
            @event => Assert.IsType<ApprovalStageRejectedDomainEvent>(@event),
            @event => Assert.IsType<ApprovalWorkflowCompletedDomainEvent>(@event));
    }

    [Fact]
    public void WrongRoleWaitingStageStaleTokenAndRepeatedActionAreRejectedAtomically()
    {
        ApprovalWorkflow workflow = ApprovalWorkflowTestData.Workflow();
        ApprovalStage pending = workflow.Stages[0];
        ApprovalStage waiting = workflow.Stages[1];

        AssertCode(ApprovalErrorCodes.RoleMismatch, () => workflow.Approve(
            pending.Id,
            PolicyApproverRole.SeniorApprover,
            ApprovalWorkflowTestData.ActorId,
            null,
            pending.ConcurrencyToken,
            ApprovalWorkflowTestData.Token(20),
            ApprovalWorkflowTestData.Token(21),
            PurchaseRequestBuilder.DefaultTime));
        AssertCode(ApprovalErrorCodes.NotActionable, () => workflow.Approve(
            waiting.Id,
            waiting.RequiredRole,
            ApprovalWorkflowTestData.ActorId,
            null,
            waiting.ConcurrencyToken,
            ApprovalWorkflowTestData.Token(20),
            ApprovalWorkflowTestData.Token(21),
            PurchaseRequestBuilder.DefaultTime));
        AssertCode(DomainErrorCodes.ConcurrencyConflict, () => workflow.Approve(
            pending.Id,
            pending.RequiredRole,
            ApprovalWorkflowTestData.ActorId,
            null,
            ApprovalWorkflowTestData.Token(99),
            ApprovalWorkflowTestData.Token(20),
            ApprovalWorkflowTestData.Token(21),
            PurchaseRequestBuilder.DefaultTime));

        workflow.Reject(
            pending.Id,
            pending.RequiredRole,
            ApprovalWorkflowTestData.ActorId,
            "Rejected.",
            pending.ConcurrencyToken,
            ApprovalWorkflowTestData.Token(20),
            PurchaseRequestBuilder.DefaultTime);
        AssertCode(ApprovalErrorCodes.NotActionable, () => workflow.Reject(
            pending.Id,
            pending.RequiredRole,
            ApprovalWorkflowTestData.ActorId,
            "Again.",
            pending.ConcurrencyToken,
            ApprovalWorkflowTestData.Token(22),
            PurchaseRequestBuilder.DefaultTime));
    }

    [Fact]
    public void OverridePreservesOriginalDecisionAndRequiresReasonAndFreshToken()
    {
        ApprovalWorkflow workflow = ApprovalWorkflowTestData.Workflow();
        ApprovalStage pending = workflow.CurrentStage!;
        ConcurrencyToken originalToken = pending.ConcurrencyToken;
        workflow.ClearDomainEvents();

        AssertCode(ApprovalErrorCodes.OverrideReasonRequired, () => workflow.OverrideDecision(
            ApprovalOutcome.Approved,
            ApprovalWorkflowTestData.ActorId,
            string.Empty,
            originalToken,
            ApprovalWorkflowTestData.Token(20),
            PurchaseRequestBuilder.DefaultTime));
        AssertCode(DomainErrorCodes.ConcurrencyConflict, () => workflow.OverrideDecision(
            ApprovalOutcome.Approved,
            ApprovalWorkflowTestData.ActorId,
            "Emergency authorization.",
            ApprovalWorkflowTestData.Token(99),
            ApprovalWorkflowTestData.Token(20),
            PurchaseRequestBuilder.DefaultTime));
        AssertCode(DomainErrorCodes.Validation, () => workflow.OverrideDecision(
            ApprovalOutcome.Approved,
            Guid.Empty,
            "Emergency authorization.",
            originalToken,
            ApprovalWorkflowTestData.Token(20),
            PurchaseRequestBuilder.DefaultTime));
        Assert.Equal(ApprovalStageStatus.Pending, pending.Status);

        workflow.OverrideDecision(
            ApprovalOutcome.Approved,
            ApprovalWorkflowTestData.ActorId,
            "  Emergency authorization.  ",
            originalToken,
            ApprovalWorkflowTestData.Token(20),
            PurchaseRequestBuilder.DefaultTime.AddMinutes(1));

        Assert.Equal(DecisionDisposition.ManualApprovalRequired, workflow.OriginalDisposition);
        Assert.Equal(ApprovalWorkflowStatus.Approved, workflow.Status);
        Assert.NotNull(workflow.Override);
        Assert.Equal(DecisionDisposition.ManualApprovalRequired, workflow.Override!.OriginalDisposition);
        Assert.Equal(ApprovalOutcome.Approved, workflow.Override.Outcome);
        Assert.Equal("Emergency authorization.", workflow.Override.Reason);
        Assert.All(
            workflow.Stages.Where(stage => stage.Status != ApprovalStageStatus.Approved),
            stage => Assert.Equal(ApprovalStageStatus.Cancelled, stage.Status));
        DecisionOverrideRecordedDomainEvent auditSource =
            Assert.IsType<DecisionOverrideRecordedDomainEvent>(workflow.DomainEvents[0]);
        Assert.Equal("Emergency authorization.", auditSource.Reason);
        Assert.IsType<ApprovalWorkflowCompletedDomainEvent>(workflow.DomainEvents[1]);
        AssertCode(ApprovalErrorCodes.NotActionable, () => workflow.OverrideDecision(
            ApprovalOutcome.Rejected,
            ApprovalWorkflowTestData.ActorId,
            "Repeat.",
            pending.ConcurrencyToken,
            ApprovalWorkflowTestData.Token(22),
            PurchaseRequestBuilder.DefaultTime.AddMinutes(2)));
    }

    [Fact]
    public void InvalidTimeTokenAndExcessiveTextDoNotPartiallyMutate()
    {
        ApprovalWorkflow workflow = ApprovalWorkflowTestData.Workflow();
        ApprovalStage stage = workflow.CurrentStage!;
        ConcurrencyToken token = stage.ConcurrencyToken;
        ApprovalStage waiting = workflow.Stages[1];

        Assert.Throws<DomainRuleException>(() => workflow.Approve(
            stage.Id,
            stage.RequiredRole,
            Guid.Empty,
            null,
            token,
            ApprovalWorkflowTestData.Token(20),
            ApprovalWorkflowTestData.Token(21),
            PurchaseRequestBuilder.DefaultTime));
        Assert.Throws<DomainRuleException>(() => workflow.Approve(
            stage.Id,
            stage.RequiredRole,
            ApprovalWorkflowTestData.ActorId,
            null,
            token,
            ApprovalWorkflowTestData.Token(20),
            waiting.ConcurrencyToken,
            PurchaseRequestBuilder.DefaultTime));
        Assert.Throws<DomainRuleException>(() => workflow.Approve(
            stage.Id,
            stage.RequiredRole,
            ApprovalWorkflowTestData.ActorId,
            new string('a', ApprovalWorkflow.MaximumNoteLength + 1),
            token,
            ApprovalWorkflowTestData.Token(20),
            ApprovalWorkflowTestData.Token(21),
            PurchaseRequestBuilder.DefaultTime));
        Assert.Throws<DomainRuleException>(() => workflow.Approve(
            stage.Id,
            stage.RequiredRole,
            ApprovalWorkflowTestData.ActorId,
            null,
            token,
            token,
            ApprovalWorkflowTestData.Token(21),
            PurchaseRequestBuilder.DefaultTime));
        Assert.Throws<DomainRuleException>(() => workflow.Approve(
            stage.Id,
            stage.RequiredRole,
            ApprovalWorkflowTestData.ActorId,
            null,
            token,
            ApprovalWorkflowTestData.Token(20),
            ApprovalWorkflowTestData.Token(21),
            PurchaseRequestBuilder.DefaultTime.AddMinutes(-1)));
        Assert.Equal(ApprovalStageStatus.Pending, stage.Status);
        Assert.Equal(token, stage.ConcurrencyToken);
        Assert.Equal(ApprovalStageStatus.Waiting, waiting.Status);
    }

    private static void AssertCode(string code, Action action)
    {
        DomainRuleException exception = Assert.Throws<DomainRuleException>(action);
        Assert.Equal(code, exception.Code);
    }
}
