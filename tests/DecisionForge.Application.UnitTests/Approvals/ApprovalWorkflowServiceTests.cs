using DecisionForge.Application.Approvals;
using DecisionForge.Application.UnitTests.PurchaseRequests;
using DecisionForge.Domain.Approvals;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.UnitTests.Approvals;

public sealed class ApprovalWorkflowServiceTests
{
    [Fact]
    public async Task AuthorizedApprovalsProgressInOrderAndFinalActionCompletesRequest()
    {
        ApprovalActionState state = await ApprovalServiceHarness.CreateStateAsync();
        ApprovalServiceHarness harness = new(state);
        ApprovalStage first = state.Workflow.CurrentStage!;
        ConcurrencyToken firstToken = first.ConcurrencyToken;
        using CancellationTokenSource source = new();

        ApprovalActionResult intermediate = await harness.Service.ApproveAsync(
            new ApproveApprovalStageCommand(first.Id, firstToken, "Reviewed."),
            source.Token);

        Assert.Same(state.Workflow, intermediate.Workflow);
        Assert.Equal(ApprovalWorkflowStatus.Active, state.Workflow.Status);
        Assert.Equal(PurchaseRequestStatus.PendingApproval, state.PurchaseRequest.Status);
        Assert.Equal(1, harness.Transaction.CommitCalls);
        Assert.Equal(source.Token, harness.Transaction.LastCancellationToken);
        ApprovalStage final = state.Workflow.CurrentStage!;
        harness.Authorization.Roles = [final.RequiredRole];

        await harness.Service.ApproveAsync(
            new ApproveApprovalStageCommand(final.Id, final.ConcurrencyToken, null),
            CancellationToken.None);

        Assert.Equal(ApprovalWorkflowStatus.Approved, state.Workflow.Status);
        Assert.Equal(PurchaseRequestStatus.Approved, state.PurchaseRequest.Status);
        Assert.Equal(2, harness.Transaction.CommitCalls);
    }

    [Fact]
    public async Task AuthorizedRejectionTerminatesWorkflowAndRequestAtomically()
    {
        ApprovalActionState state = await ApprovalServiceHarness.CreateStateAsync();
        ApprovalServiceHarness harness = new(state);
        ApprovalStage stage = state.Workflow.CurrentStage!;

        ApprovalActionResult result = await harness.Service.RejectAsync(
            new RejectApprovalStageCommand(
                stage.Id,
                stage.ConcurrencyToken,
                "Insufficient evidence."),
            CancellationToken.None);

        Assert.Equal(ApprovalWorkflowStatus.Rejected, result.Workflow.Status);
        Assert.Equal(PurchaseRequestStatus.Rejected, result.PurchaseRequest.Status);
        Assert.Equal(1, harness.Transaction.CommitCalls);
        Assert.All(
            state.Workflow.Stages.Skip(1),
            future => Assert.Equal(ApprovalStageStatus.Skipped, future.Status));
    }

    [Fact]
    public async Task RoleComesFromTrustedAuthorizationAndWrongRoleCannotMutate()
    {
        ApprovalActionState state = await ApprovalServiceHarness.CreateStateAsync();
        ApprovalServiceHarness harness = new(state);
        ApprovalStage stage = state.Workflow.CurrentStage!;
        harness.Authorization.Roles = [PolicyApproverRole.SeniorApprover];
        ConcurrencyToken originalToken = stage.ConcurrencyToken;

        DomainRuleException exception = await Assert.ThrowsAsync<DomainRuleException>(
            () => harness.Service.ApproveAsync(
                new ApproveApprovalStageCommand(stage.Id, originalToken, null),
                CancellationToken.None));

        Assert.Equal(ApprovalApplicationErrorCodes.Forbidden, exception.Code);
        Assert.Equal(ApprovalStageStatus.Pending, stage.Status);
        Assert.Equal(originalToken, stage.ConcurrencyToken);
        Assert.Equal(0, harness.Transaction.CommitCalls);
        Assert.Equal(ApprovalServiceHarness.ActorId, harness.Authorization.UserId);
    }

    [Fact]
    public async Task StaleAndRepeatedActionsCannotCommitDuplicateOutcomes()
    {
        ApprovalActionState state = await ApprovalServiceHarness.CreateStateAsync();
        ApprovalServiceHarness harness = new(state);
        ApprovalStage stage = state.Workflow.CurrentStage!;
        ConcurrencyToken originalToken = stage.ConcurrencyToken;

        DomainRuleException stale = await Assert.ThrowsAsync<DomainRuleException>(
            () => harness.Service.ApproveAsync(
                new ApproveApprovalStageCommand(
                    stage.Id,
                    ConcurrencyToken.Create(Guid.Parse("10101010-1010-4010-8010-101010101010")),
                    null),
                CancellationToken.None));
        Assert.Equal(DomainErrorCodes.ConcurrencyConflict, stale.Code);
        Assert.Equal(0, harness.Transaction.CommitCalls);

        await harness.Service.RejectAsync(
            new RejectApprovalStageCommand(stage.Id, originalToken, "Rejected."),
            CancellationToken.None);
        DomainRuleException repeated = await Assert.ThrowsAsync<DomainRuleException>(
            () => harness.Service.RejectAsync(
                new RejectApprovalStageCommand(stage.Id, stage.ConcurrencyToken, "Again."),
                CancellationToken.None));
        Assert.Equal(ApprovalErrorCodes.NotActionable, repeated.Code);
        Assert.Equal(1, harness.Transaction.CommitCalls);
    }

    [Fact]
    public async Task OverrideRequiresExplicitPermissionAndPreservesDecisionOutcome()
    {
        ApprovalActionState state = await ApprovalServiceHarness.CreateStateAsync();
        ApprovalServiceHarness harness = new(state);
        ApprovalStage current = state.Workflow.CurrentStage!;

        DomainRuleException denied = await Assert.ThrowsAsync<DomainRuleException>(
            () => harness.Service.OverrideAsync(
                new OverrideApprovalWorkflowCommand(
                    state.Workflow.Id,
                    ApprovalOutcome.Approved,
                    current.ConcurrencyToken,
                    "Emergency approval."),
                CancellationToken.None));
        Assert.Equal(ApprovalApplicationErrorCodes.Forbidden, denied.Code);
        Assert.Equal(0, harness.Transaction.CommitCalls);

        harness.Authorization.CanOverride = true;
        ApprovalActionResult result = await harness.Service.OverrideAsync(
            new OverrideApprovalWorkflowCommand(
                state.Workflow.Id,
                ApprovalOutcome.Approved,
                current.ConcurrencyToken,
                "Emergency approval."),
            CancellationToken.None);

        Assert.Equal(DecisionDisposition.ManualApprovalRequired, result.Workflow.OriginalDisposition);
        Assert.Equal(ApprovalOutcome.Approved, result.Workflow.Override!.Outcome);
        Assert.Equal(PurchaseRequestStatus.Approved, result.PurchaseRequest.Status);
        Assert.Equal(1, harness.Transaction.CommitCalls);
        Assert.Equal(2, harness.Authorization.OverrideCalls);
    }

    [Fact]
    public async Task MissingResourcesUnauthenticatedUsersAndCancellationFailBeforeMutation()
    {
        ApprovalActionState state = await ApprovalServiceHarness.CreateStateAsync();
        ApprovalStage stage = state.Workflow.CurrentStage!;
        ApprovalServiceHarness missing = new(state);
        missing.Transaction.Existing = null;
        DomainRuleException notFound = await Assert.ThrowsAsync<DomainRuleException>(
            () => missing.Service.ApproveAsync(
                new ApproveApprovalStageCommand(stage.Id, stage.ConcurrencyToken, null),
                CancellationToken.None));
        Assert.Equal(ApprovalApplicationErrorCodes.NotFound, notFound.Code);

        ApprovalServiceHarness anonymous = new(state);
        anonymous.CurrentUser.UserId = null;
        DomainRuleException unauthenticated = await Assert.ThrowsAsync<DomainRuleException>(
            () => anonymous.Service.ApproveAsync(
                new ApproveApprovalStageCommand(stage.Id, stage.ConcurrencyToken, null),
                CancellationToken.None));
        Assert.Equal(ApprovalApplicationErrorCodes.Unauthenticated, unauthenticated.Code);

        ApprovalServiceHarness cancelled = new(state);
        using CancellationTokenSource source = new();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cancelled.Service.ApproveAsync(
                new ApproveApprovalStageCommand(stage.Id, stage.ConcurrencyToken, null),
                source.Token));
        Assert.Equal(0, cancelled.Transaction.StageFindCalls);
        Assert.Equal(0, cancelled.Transaction.CommitCalls);
    }
}
