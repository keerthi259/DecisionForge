namespace DecisionForge.Infrastructure.Identity;

public sealed class IdentitySeedOptions
{
    public const string SectionName = "DecisionForge:Identity:Seeding";

    public bool SeedRolesOnStartup { get; init; }

    public DemoIdentityOptions Demo { get; init; } = new();

    public bool IsValid()
    {
        return !Demo.Enabled || Demo.HasStrongConfiguredPassword();
    }
}

public sealed class DemoIdentityOptions
{
    public bool Enabled { get; init; }

    public string? Password { get; init; }

    internal bool HasStrongConfiguredPassword()
    {
        return Password is { Length: >= 12 and <= 256 }
            && Password.Any(char.IsUpper)
            && Password.Any(char.IsLower)
            && Password.Any(char.IsDigit)
            && Password.Any(character => !char.IsLetterOrDigit(character));
    }
}
