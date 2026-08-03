using DecisionForge.Application.Approvals;
using DecisionForge.Application.Approvals.Ports;
using DecisionForge.Application.Decisions;
using DecisionForge.Application.UnitTests.Decisions;
using DecisionForge.Application.UnitTests.PurchaseRequests;
using DecisionForge.Domain.Approvals;
using DecisionForge.Domain.Policies;
using DecisionForge.Domain.PurchaseRequests;

namespace DecisionForge.Application.UnitTests.Approvals;

internal sealed class StubApprovalAuthorization : IApprovalAuthorization
{
    public IReadOnlyCollection<PolicyApproverRole> Roles { get; set; } = [];

    public bool CanOverride { get; set; }

    public int RoleCalls { get; private set; }

    public int OverrideCalls { get; private set; }

    public Guid? UserId { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task<IReadOnlyCollection<PolicyApproverRole>> GetApproverRolesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        RoleCalls++;
        UserId = userId;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(Roles);
    }

    public Task<bool> CanOverrideDecisionAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        OverrideCalls++;
        UserId = userId;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(CanOverride);
    }
}

internal sealed class RecordingApprovalActionTransaction : IApprovalActionTransaction
{
    public ApprovalActionState? Existing { get; set; }

    public int StageFindCalls { get; private set; }

    public int WorkflowFindCalls { get; private set; }

    public int CommitCalls { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task<ApprovalActionState?> FindByStageIdAsync(
        Guid stageId,
        CancellationToken cancellationToken)
    {
        StageFindCalls++;
        LastCancellationToken = cancellationToken;
        ApprovalActionState? result = Existing?.Workflow.Stages.Any(stage => stage.Id == stageId) == true
            ? Existing
            : null;
        return Task.FromResult(result);
    }

    public Task<ApprovalActionState?> FindByWorkflowIdAsync(
        Guid workflowId,
        CancellationToken cancellationToken)
    {
        WorkflowFindCalls++;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(Existing?.Workflow.Id == workflowId ? Existing : null);
    }

    public Task CommitAsync(
        ApprovalWorkflow workflow,
        PurchaseRequest purchaseRequest,
        CancellationToken cancellationToken)
    {
        CommitCalls++;
        LastCancellationToken = cancellationToken;
        return Task.CompletedTask;
    }
}

internal sealed class RecordingApprovalQueries : IApprovalQueries
{
    public ApprovalInboxResult InboxResult { get; set; } = new([], 0, 0, 20);

    public ApprovalWorkflowDetail? Detail { get; set; }

    public int ListCalls { get; private set; }

    public int DetailCalls { get; private set; }

    public Guid? UserId { get; private set; }

    public IReadOnlyCollection<PolicyApproverRole>? Roles { get; private set; }

    public ApprovalInboxPage? Page { get; private set; }

    public bool CanOverride { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task<ApprovalInboxResult> ListForAuthorizedRolesAsync(
        Guid userId,
        IReadOnlyCollection<PolicyApproverRole> authorizedRoles,
        ApprovalInboxPage page,
        CancellationToken cancellationToken)
    {
        ListCalls++;
        UserId = userId;
        Roles = authorizedRoles;
        Page = page;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(InboxResult);
    }

    public Task<ApprovalWorkflowDetail?> FindForAuthorizedRolesAsync(
        Guid workflowId,
        Guid userId,
        IReadOnlyCollection<PolicyApproverRole> authorizedRoles,
        bool canOverrideDecision,
        CancellationToken cancellationToken)
    {
        DetailCalls++;
        UserId = userId;
        Roles = authorizedRoles;
        CanOverride = canOverrideDecision;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(Detail?.Id == workflowId ? Detail : null);
    }
}

internal sealed class ApprovalServiceHarness
{
    public static readonly Guid ActorId = Guid.Parse("99999999-9999-4999-8999-999999999999");

    public ApprovalServiceHarness(ApprovalActionState state)
    {
        Transaction.Existing = state;
        Authorization.Roles = [state.Workflow.CurrentStage!.RequiredRole];
        Service = new ApprovalWorkflowService(
            Transaction,
            Authorization,
            CurrentUser,
            IdGenerator,
            TimeProvider);
    }

    public RecordingApprovalActionTransaction Transaction { get; } = new();

    public StubApprovalAuthorization Authorization { get; } = new();

    public StubCurrentUser CurrentUser { get; } = new(ActorId);

    public RequestSequenceIdGenerator IdGenerator { get; } = new(
        Enumerable.Range(600, 30)
            .Select(sequence => Guid.Parse($"99999999-9999-4999-8999-{sequence:000000000000}"))
            .ToArray());

    public RequestFixedTimeProvider TimeProvider { get; } = new(
        PurchaseRequestApplicationTestData.CurrentTime.AddHours(1));

    public ApprovalWorkflowService Service { get; }

    public static async Task<ApprovalActionState> CreateStateAsync()
    {
        PurchaseRequest request = PurchaseRequestApplicationTestData.CreateRequest(withItem: true);
        DecisionServiceHarness decisionHarness = new(
            request,
            DecisionApplicationTestData.Source());
        _ = await decisionHarness.Service.SubmitAsync(
            new SubmitPurchaseRequestForDecisionCommand(
                request.Id,
                request.ConcurrencyToken,
                Domain.ValueObjects.IdempotencyKey.Parse("approval-setup")),
            CancellationToken.None);
        return new ApprovalActionState(
            decisionHarness.Transaction.ApprovalWorkflow!,
            request);
    }
}
