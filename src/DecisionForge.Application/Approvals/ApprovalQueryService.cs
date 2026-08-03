using DecisionForge.Application.Approvals.Ports;
using DecisionForge.Application.Platform;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Policies;

namespace DecisionForge.Application.Approvals;

public sealed class ApprovalQueryService
{
    private readonly IApprovalQueries _queries;
    private readonly IApprovalAuthorization _authorization;
    private readonly ICurrentUserContext _currentUser;

    public ApprovalQueryService(
        IApprovalQueries queries,
        IApprovalAuthorization authorization,
        ICurrentUserContext currentUser)
    {
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(currentUser);
        _queries = queries;
        _authorization = authorization;
        _currentUser = currentUser;
    }

    public async Task<ApprovalInboxResult> ListInboxAsync(
        ListApprovalInboxQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        Guid userId = RequiredUserId();
        ApprovalInboxPage page = ApprovalInboxPage.Create(
            query.Offset,
            query.PageSize,
            query.SortOrder);
        IReadOnlyCollection<PolicyApproverRole> roles = await GetRolesAsync(
            userId,
            cancellationToken);
        IReadOnlyCollection<PolicyApproverRole> selectedRoles = SelectRoles(
            roles,
            query.RequiredRole);
        if (selectedRoles.Count == 0)
        {
            return new ApprovalInboxResult([], 0, page.Offset, page.PageSize);
        }

        return await _queries.ListForAuthorizedRolesAsync(
            userId,
            selectedRoles,
            page,
            cancellationToken);
    }

    public async Task<ApprovalWorkflowDetail> GetDetailAsync(
        GetApprovalWorkflowDetailQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        Guid userId = RequiredUserId();
        IReadOnlyCollection<PolicyApproverRole> roles = await GetRolesAsync(
            userId,
            cancellationToken);
        bool canOverride = await _authorization.CanOverrideDecisionAsync(
            userId,
            cancellationToken);
        ApprovalWorkflowDetail? detail = await _queries.FindForAuthorizedRolesAsync(
            query.WorkflowId,
            userId,
            roles,
            canOverride,
            cancellationToken);
        if (detail is null
            || (!canOverride && !detail.Stages.Any(stage => roles.Contains(stage.RequiredRole))))
        {
            throw new DomainRuleException(
                ApprovalApplicationErrorCodes.NotFound,
                $"Approval workflow '{query.WorkflowId}' was not found.",
                nameof(query.WorkflowId));
        }

        return detail;
    }

    private async Task<IReadOnlyCollection<PolicyApproverRole>> GetRolesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<PolicyApproverRole> roles =
            await _authorization.GetApproverRolesAsync(userId, cancellationToken);
        if (roles is null || roles.Any(role => !Enum.IsDefined(role)))
        {
            throw new DomainRuleException(
                ApprovalApplicationErrorCodes.Forbidden,
                "The current user's approval role scope is invalid.");
        }

        return Array.AsReadOnly(roles.Distinct().ToArray());
    }

    private static IReadOnlyCollection<PolicyApproverRole> SelectRoles(
        IReadOnlyCollection<PolicyApproverRole> authorizedRoles,
        PolicyApproverRole? requestedRole)
    {
        if (requestedRole is null)
        {
            return authorizedRoles;
        }

        if (!Enum.IsDefined(requestedRole.Value)
            || !authorizedRoles.Contains(requestedRole.Value))
        {
            throw new DomainRuleException(
                ApprovalApplicationErrorCodes.Forbidden,
                "The requested approval-role filter is not authorized.",
                nameof(requestedRole));
        }

        return [requestedRole.Value];
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
}
