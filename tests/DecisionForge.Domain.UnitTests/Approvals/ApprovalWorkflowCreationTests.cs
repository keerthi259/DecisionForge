using DecisionForge.Domain.Approvals;
using DecisionForge.Domain.Approvals.Events;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Decisions;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies;
using DecisionForge.Domain.UnitTests.Builders;

namespace DecisionForge.Domain.UnitTests.Approvals;

public sealed class ApprovalWorkflowCreationTests
{
    [Fact]
    public void ManualDecisionCreatesOrderedStagesWithExactlyOnePending()
    {
        ApprovalWorkflow workflow = ApprovalWorkflowTestData.Workflow();

        Assert.Equal(ApprovalWorkflowStatus.Active, workflow.Status);
        Assert.Equal(DecisionDisposition.ManualApprovalRequired, workflow.OriginalDisposition);
        Assert.Equal(
            [PolicyApproverRole.DepartmentApprover, PolicyApproverRole.SecurityApprover, PolicyApproverRole.FinanceApprover],
            workflow.Stages.Select(stage => stage.RequiredRole));
        Assert.Equal([1, 2, 3], workflow.Stages.Select(stage => stage.Sequence));
        Assert.Equal(ApprovalStageStatus.Pending, workflow.Stages[0].Status);
        Assert.All(workflow.Stages.Skip(1), stage => Assert.Equal(ApprovalStageStatus.Waiting, stage.Status));
        Assert.Single(workflow.Stages, stage => stage.Status == ApprovalStageStatus.Pending);
        Assert.Equal(workflow.Stages[0], workflow.CurrentStage);
        Assert.Collection(
            workflow.DomainEvents,
            @event => Assert.IsType<ApprovalWorkflowCreatedDomainEvent>(@event),
            @event => Assert.IsType<ApprovalStageActivatedDomainEvent>(@event));
        Assert.Throws<NotSupportedException>(() => ((IList<ApprovalStage>)workflow.Stages).Clear());
    }

    [Theory]
    [InlineData(DecisionDisposition.AutoApproved)]
    [InlineData(DecisionDisposition.Rejected)]
    public void NonManualDecisionCannotCreateWorkflow(DecisionDisposition disposition)
    {
        DomainRuleException exception = Assert.Throws<DomainRuleException>(() =>
            ApprovalWorkflow.Create(
                ApprovalWorkflowTestData.WorkflowId,
                ApprovalWorkflowTestData.Decision(disposition),
                [ApprovalWorkflowTestData.StageId(0)],
                [ApprovalWorkflowTestData.Token(0)],
                PurchaseRequestBuilder.DefaultTime));

        Assert.Equal(ApprovalErrorCodes.ManualDecisionRequired, exception.Code);
    }

    [Fact]
    public void StageIdentityMustMatchThePlanAndBeUnique()
    {
        Decision decision = ApprovalWorkflowTestData.Decision(
            DecisionDisposition.ManualApprovalRequired,
            [PolicyApproverRole.DepartmentApprover, PolicyApproverRole.FinanceApprover]);

        Assert.Throws<DomainRuleException>(() => ApprovalWorkflow.Create(
            ApprovalWorkflowTestData.WorkflowId,
            decision,
            [ApprovalWorkflowTestData.StageId(0)],
            [ApprovalWorkflowTestData.Token(0)],
            PurchaseRequestBuilder.DefaultTime));
        Assert.Throws<DomainRuleException>(() => ApprovalWorkflow.Create(
            ApprovalWorkflowTestData.WorkflowId,
            decision,
            [ApprovalWorkflowTestData.StageId(0), ApprovalWorkflowTestData.StageId(0)],
            [ApprovalWorkflowTestData.Token(0), ApprovalWorkflowTestData.Token(1)],
            PurchaseRequestBuilder.DefaultTime));
        Assert.Throws<DomainRuleException>(() => ApprovalWorkflow.Create(
            ApprovalWorkflowTestData.WorkflowId,
            decision,
            [ApprovalWorkflowTestData.StageId(0), ApprovalWorkflowTestData.StageId(1)],
            [ApprovalWorkflowTestData.Token(0), ApprovalWorkflowTestData.Token(0)],
            PurchaseRequestBuilder.DefaultTime));
    }
}
