using System.Security.Claims;
using DecisionForge.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;

namespace DecisionForge.Api.Authorization;

public sealed class PurchaseRequestAuthorizationHandler
    : AuthorizationHandler<PurchaseRequestAuthorizationRequirement, PurchaseRequestAuthorizationResource>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PurchaseRequestAuthorizationRequirement requirement,
        PurchaseRequestAuthorizationResource resource)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        bool isRequester = context.User.IsInRole(DecisionForgeIdentityRoles.Requester);
        bool isOwner = isRequester && UserId(context.User) == resource.RequesterId;
        bool allowed = requirement.Operation switch
        {
            PurchaseRequestAuthorizationOperation.Read =>
                isOwner
                || context.User.IsInRole(DecisionForgeIdentityRoles.Auditor)
                || (resource.AssignedApproverRoles is not null
                    && resource.AssignedApproverRoles.Any(context.User.IsInRole)),
            PurchaseRequestAuthorizationOperation.Edit or
                PurchaseRequestAuthorizationOperation.Submit => isOwner && resource.IsDraft,
            _ => false,
        };
        if (allowed)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static Guid? UserId(ClaimsPrincipal principal)
    {
        return Guid.TryParse(
            principal.FindFirstValue(ClaimTypes.NameIdentifier),
            out Guid userId)
            ? userId
            : null;
    }
}
