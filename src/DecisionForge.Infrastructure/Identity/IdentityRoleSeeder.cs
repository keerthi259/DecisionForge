using DecisionForge.Application.Platform;
using Microsoft.AspNetCore.Identity;

namespace DecisionForge.Infrastructure.Identity;

public sealed class IdentityRoleSeeder(
    RoleManager<IdentityRole<Guid>> roleManager,
    IIdGenerator idGenerator)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        foreach (string roleName in DecisionForgeIdentityRoles.All)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await roleManager.RoleExistsAsync(roleName).WaitAsync(cancellationToken))
            {
                continue;
            }

            IdentityResult result = await roleManager.CreateAsync(
                new IdentityRole<Guid>(roleName) { Id = idGenerator.Create() })
                .WaitAsync(cancellationToken);
            if (!result.Succeeded
                && await roleManager.RoleExistsAsync(roleName).WaitAsync(cancellationToken))
            {
                continue;
            }

            IdentityOperation.EnsureSucceeded(result, "identity.role-seed-failed");
        }
    }
}
