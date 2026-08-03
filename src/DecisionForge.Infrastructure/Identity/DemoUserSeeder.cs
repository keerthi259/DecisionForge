using System.Security.Claims;
using DecisionForge.Application.Platform;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DecisionForge.Infrastructure.Identity;

public sealed class DemoUserSeeder(
    UserManager<DecisionForgeUser> userManager,
    IIdGenerator idGenerator,
    IOptions<IdentitySeedOptions> options,
    IHostEnvironment environment)
{
    private static readonly DemoUserDefinition[] _definitions =
    [
        new("requester@decisionforge.local", "Demo Requester", [DecisionForgeIdentityRoles.Requester], []),
        new("department@decisionforge.local", "Department Approver", [DecisionForgeIdentityRoles.DepartmentApprover], []),
        new("procurement@decisionforge.local", "Procurement Approver", [DecisionForgeIdentityRoles.ProcurementApprover], []),
        new("security@decisionforge.local", "Security Approver", [DecisionForgeIdentityRoles.SecurityApprover], []),
        new("finance@decisionforge.local", "Finance Approver", [DecisionForgeIdentityRoles.FinanceApprover], []),
        new("senior@decisionforge.local", "Senior Approver", [DecisionForgeIdentityRoles.SeniorApprover], [DecisionForgeIdentityPermissions.OverrideDecision]),
        new("author@decisionforge.local", "Policy Author", [DecisionForgeIdentityRoles.PolicyAuthor], []),
        new("publisher@decisionforge.local", "Policy Publisher", [DecisionForgeIdentityRoles.PolicyPublisher], []),
        new("auditor@decisionforge.local", "Auditor", [DecisionForgeIdentityRoles.Auditor], []),
        new("administrator@decisionforge.local", "Administrator", [DecisionForgeIdentityRoles.Administrator], []),
    ];

    public static int UserCount => _definitions.Length;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        IdentitySeedOptions settings = options.Value;
        if (!settings.Demo.Enabled || !IsDemoEnvironment(environment.EnvironmentName))
        {
            return;
        }

        string password = settings.Demo.Password
            ?? throw new InvalidOperationException("identity.demo-password-required");
        foreach (DemoUserDefinition definition in _definitions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DecisionForgeUser user = await FindOrCreateAsync(
                definition,
                password,
                cancellationToken);
            await EnsureRolesAsync(user, definition.Roles, cancellationToken);
            await EnsurePermissionsAsync(user, definition.Permissions, cancellationToken);
        }
    }

    internal static bool IsDemoEnvironment(string environmentName)
    {
        return string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase)
            || string.Equals(environmentName, "Demo", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<DecisionForgeUser> FindOrCreateAsync(
        DemoUserDefinition definition,
        string password,
        CancellationToken cancellationToken)
    {
        DecisionForgeUser? existing = await userManager.FindByEmailAsync(definition.Email)
            .WaitAsync(cancellationToken);
        if (existing is not null)
        {
            if (!existing.IsDemo)
            {
                throw new InvalidOperationException("identity.demo-user-collision");
            }

            return existing;
        }

        DecisionForgeUser user = new()
        {
            Id = idGenerator.Create(),
            UserName = definition.Email,
            Email = definition.Email,
            DisplayName = definition.DisplayName,
            EmailConfirmed = true,
            LockoutEnabled = true,
            IsDemo = true,
        };
        IdentityResult result = await userManager.CreateAsync(user, password)
            .WaitAsync(cancellationToken);
        IdentityOperation.EnsureSucceeded(result, "identity.demo-user-create-failed");
        return user;
    }

    private async Task EnsureRolesAsync(
        DecisionForgeUser user,
        IReadOnlyCollection<string> requiredRoles,
        CancellationToken cancellationToken)
    {
        IList<string> currentRoles = await userManager.GetRolesAsync(user)
            .WaitAsync(cancellationToken);
        string[] missingRoles = requiredRoles.Except(currentRoles, StringComparer.Ordinal).ToArray();
        if (missingRoles.Length == 0)
        {
            return;
        }

        IdentityResult result = await userManager.AddToRolesAsync(user, missingRoles)
            .WaitAsync(cancellationToken);
        IdentityOperation.EnsureSucceeded(result, "identity.demo-role-assignment-failed");
    }

    private async Task EnsurePermissionsAsync(
        DecisionForgeUser user,
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken)
    {
        IList<Claim> claims = await userManager.GetClaimsAsync(user).WaitAsync(cancellationToken);
        foreach (string permission in permissions)
        {
            if (claims.Any(claim => claim.Type == DecisionForgeIdentityPermissions.ClaimType
                    && claim.Value == permission))
            {
                continue;
            }

            IdentityResult result = await userManager.AddClaimAsync(
                user,
                new Claim(DecisionForgeIdentityPermissions.ClaimType, permission))
                .WaitAsync(cancellationToken);
            IdentityOperation.EnsureSucceeded(result, "identity.demo-claim-assignment-failed");
        }
    }

    private sealed record DemoUserDefinition(
        string Email,
        string DisplayName,
        IReadOnlyCollection<string> Roles,
        IReadOnlyCollection<string> Permissions);
}
