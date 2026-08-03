using Microsoft.AspNetCore.Authorization;

namespace DecisionForge.Api.Authorization;

public sealed class ApprovalStageAuthorizationHandler
    : AuthorizationHandler<ActOnApprovalStageRequirement, ApprovalStageAuthorizationResource>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActOnApprovalStageRequirement requirement,
        ApprovalStageAuthorizationResource resource)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && resource.IsPending
            && !string.IsNullOrWhiteSpace(resource.RequiredRole)
            && context.User.IsInRole(resource.RequiredRole))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
