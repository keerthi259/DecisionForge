using DecisionForge.Application.Approvals.Ports;
using DecisionForge.Application.Platform;
using DecisionForge.Domain.Approvals;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.Approvals;

public sealed class ApprovalWorkflowService
{
    private readonly IApprovalActionTransaction _transaction;
    private readonly IApprovalAuthorization _authorization;
    private readonly ICurrentUserContext _currentUser;
    private readonly IIdGenerator _idGenerator;
    private readonly TimeProvider _timeProvider;

    public ApprovalWorkflowService(
        IApprovalActionTransaction transaction,
        IApprovalAuthorization authorization,
        ICurrentUserContext currentUser,
        IIdGenerator idGenerator,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(idGenerator);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _transaction = transaction;
        _authorization = authorization;
        _currentUser = currentUser;
        _idGenerator = idGenerator;
        _timeProvider = timeProvider;
    }

    public async Task<ApprovalActionResult> ApproveAsync(
        ApproveApprovalStageCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        Guid actorId = RequiredUserId();
        ApprovalActionState state = await FindByStageAsync(command.StageId, cancellationToken);
        ApprovalStage stage = state.Workflow.FindStage(command.StageId);
        await EnsureRoleAsync(actorId, stage.RequiredRole, cancellationToken);
        DateTimeOffset occurredAt = _timeProvider.GetUtcNow();
        state.Workflow.Approve(
            stage.Id,
            stage.RequiredRole,
            actorId,
            command.Note,
            command.ExpectedToken,
            NextToken(),
            NextToken(),
            occurredAt);
        CompleteRequestWhenTerminal(state, occurredAt);
        await _transaction.CommitAsync(
            state.Workflow,
            state.PurchaseRequest,
            cancellationToken);
        return new ApprovalActionResult(state.Workflow, state.PurchaseRequest);
    }

    public async Task<ApprovalActionResult> RejectAsync(
        RejectApprovalStageCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        Guid actorId = RequiredUserId();
        ApprovalActionState state = await FindByStageAsync(command.StageId, cancellationToken);
        ApprovalStage stage = state.Workflow.FindStage(command.StageId);
        await EnsureRoleAsync(actorId, stage.RequiredRole, cancellationToken);
        DateTimeOffset occurredAt = _timeProvider.GetUtcNow();
        state.Workflow.Reject(
            stage.Id,
            stage.RequiredRole,
            actorId,
            command.Reason,
            command.ExpectedToken,
            NextToken(),
            occurredAt);
        CompleteRequestWhenTerminal(state, occurredAt);
        await _transaction.CommitAsync(
            state.Workflow,
            state.PurchaseRequest,
            cancellationToken);
        return new ApprovalActionResult(state.Workflow, state.PurchaseRequest);
    }

    public async Task<ApprovalActionResult> OverrideAsync(
        OverrideApprovalWorkflowCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        Guid actorId = RequiredUserId();
        ApprovalActionState state = await FindByWorkflowAsync(
            command.WorkflowId,
            cancellationToken);
        bool permitted = await _authorization.CanOverrideDecisionAsync(
            actorId,
            cancellationToken);
        if (!permitted)
        {
            throw Forbidden();
        }

        DateTimeOffset occurredAt = _timeProvider.GetUtcNow();
        state.Workflow.OverrideDecision(
            command.Outcome,
            actorId,
            command.Reason,
            command.ExpectedCurrentStageToken,
            NextToken(),
            occurredAt);
        CompleteRequestWhenTerminal(state, occurredAt);
        await _transaction.CommitAsync(
            state.Workflow,
            state.PurchaseRequest,
            cancellationToken);
        return new ApprovalActionResult(state.Workflow, state.PurchaseRequest);
    }

    private async Task EnsureRoleAsync(
        Guid actorId,
        PolicyApproverRole requiredRole,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<PolicyApproverRole> roles =
            await _authorization.GetApproverRolesAsync(actorId, cancellationToken);
        if (roles is null
            || roles.Any(role => !Enum.IsDefined(role))
            || !roles.Contains(requiredRole))
        {
            throw Forbidden();
        }
    }

    private void CompleteRequestWhenTerminal(
        ApprovalActionState state,
        DateTimeOffset occurredAt)
    {
        ApprovalOutcome? outcome = state.Workflow.Status switch
        {
            ApprovalWorkflowStatus.Approved => ApprovalOutcome.Approved,
            ApprovalWorkflowStatus.Rejected => ApprovalOutcome.Rejected,
            _ => null,
        };
        if (outcome is null)
        {
            return;
        }

        state.PurchaseRequest.CompleteApproval(
            state.Workflow.Id,
            outcome.Value,
            state.PurchaseRequest.ConcurrencyToken,
            NextToken(),
            occurredAt);
    }

    private async Task<ApprovalActionState> FindByStageAsync(
        Guid stageId,
        CancellationToken cancellationToken)
    {
        ApprovalActionState? state = await _transaction.FindByStageIdAsync(
            stageId,
            cancellationToken);
        return state ?? throw NotFound(stageId, nameof(stageId));
    }

    private async Task<ApprovalActionState> FindByWorkflowAsync(
        Guid workflowId,
        CancellationToken cancellationToken)
    {
        ApprovalActionState? state = await _transaction.FindByWorkflowIdAsync(
            workflowId,
            cancellationToken);
        return state ?? throw NotFound(workflowId, nameof(workflowId));
    }

    private Guid RequiredUserId()
    {
        if (_currentUser.UserId is not { } userId || userId == Guid.Empty)
        {
            throw new DomainRuleException(
                ApprovalApplicationErrorCodes.Unauthenticated,
                "An authenticated user is required.");
        }

        return userId;
    }

    private ConcurrencyToken NextToken()
    {
        return ConcurrencyToken.Create(_idGenerator.Create());
    }

    private static DomainRuleException Forbidden()
    {
        return new DomainRuleException(
            ApprovalApplicationErrorCodes.Forbidden,
            "The current user cannot act on this approval.");
    }

    private static DomainRuleException NotFound(Guid id, string parameterName)
    {
        return new DomainRuleException(
            ApprovalApplicationErrorCodes.NotFound,
            $"Approval resource '{id}' was not found.",
            parameterName);
    }
}
