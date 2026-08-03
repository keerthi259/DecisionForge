using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace DecisionForge.Application.Reliability;

public static class SafeFreeTextEvidence
{
    public static IReadOnlyDictionary<string, string> Create(string prefix, string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        string normalized = value?.Trim() ?? string.Empty;
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [$"{prefix}Provided"] = normalized.Length == 0 ? "false" : "true",
            [$"{prefix}Length"] = normalized.Length.ToString(CultureInfo.InvariantCulture),
            [$"{prefix}Sha256"] = Convert.ToHexStringLower(
                SHA256.HashData(Encoding.UTF8.GetBytes(normalized))),
        };
    }
}
