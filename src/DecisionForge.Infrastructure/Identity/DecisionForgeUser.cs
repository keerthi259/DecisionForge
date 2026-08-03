using Microsoft.AspNetCore.Identity;

namespace DecisionForge.Infrastructure.Identity;

public sealed class DecisionForgeUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public bool IsDemo { get; set; }
}
