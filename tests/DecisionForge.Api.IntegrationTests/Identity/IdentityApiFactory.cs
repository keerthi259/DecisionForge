namespace DecisionForge.Api.IntegrationTests.Identity;

internal sealed class IdentityApiFactory(
    string connectionString,
    int loginPermitLimit = 100)
    : PostgreSqlApiFactory(
        connectionString,
        new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["DecisionForge:Identity:LoginRateLimit:PermitLimit"] = Invariant(loginPermitLimit),
            ["DecisionForge:Identity:LoginRateLimit:WindowSeconds"] = "60",
        })
{
    private static string Invariant(int value)
    {
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
