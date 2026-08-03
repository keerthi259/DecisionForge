namespace DecisionForge.Api.Identity;

public sealed class IdentityApiOptions
{
    public const string SectionName = "DecisionForge:Identity:LoginRateLimit";

    public int PermitLimit { get; init; } = 10;

    public int WindowSeconds { get; init; } = 60;

    public bool IsValid()
    {
        return PermitLimit is >= 1 and <= 100
            && WindowSeconds is >= 1 and <= 600;
    }
}
