using DecisionForge.Application.Approvals.Ports;
using DecisionForge.Domain.Policies;
using Microsoft.AspNetCore.Identity;

namespace DecisionForge.Infrastructure.Identity;

public sealed class IdentityApprovalAuthorization(
    UserManager<DecisionForgeUser> userManager) : IApprovalAuthorization
{
    public async Task<IReadOnlyCollection<PolicyApproverRole>> GetApproverRolesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DecisionForgeUser? user = await userManager.FindByIdAsync(userId.ToString("D"))
            .WaitAsync(cancellationToken);
        if (user is null)
        {
            return [];
        }

        IList<string> roleNames = await userManager.GetRolesAsync(user)
            .WaitAsync(cancellationToken);
        PolicyApproverRole[] roles = roleNames
            .Select(roleName => DecisionForgeIdentityRoles.TryGetApproverRole(
                roleName,
                out PolicyApproverRole role)
                ? role
                : (PolicyApproverRole?)null)
            .Where(role => role.HasValue)
            .Select(role => role!.Value)
            .Distinct()
            .ToArray();
        return Array.AsReadOnly(roles);
    }

    public async Task<bool> CanOverrideDecisionAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DecisionForgeUser? user = await userManager.FindByIdAsync(userId.ToString("D"))
            .WaitAsync(cancellationToken);
        if (user is null)
        {
            return false;
        }

        IList<System.Security.Claims.Claim> claims = await userManager.GetClaimsAsync(user)
            .WaitAsync(cancellationToken);
        return claims.Any(claim =>
            claim.Type == DecisionForgeIdentityPermissions.ClaimType
            && claim.Value == DecisionForgeIdentityPermissions.OverrideDecision);
    }
}
