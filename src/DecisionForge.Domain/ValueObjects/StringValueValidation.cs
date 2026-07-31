using DecisionForge.Domain.Common;

namespace DecisionForge.Domain.ValueObjects;

internal static class StringValueValidation
{
    public static string Required(string? value, int maximumLength, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > maximumLength)
        {
            throw DomainGuard.Validation(
                parameterName,
                $"{parameterName} must contain between 1 and {maximumLength} characters.");
        }

        return normalized;
    }

    public static string Code(
        string? value,
        int maximumLength,
        string parameterName,
        string additionalCharacters = "-_")
    {
        string normalized = Required(value, maximumLength, parameterName).ToUpperInvariant();
        if (normalized.Any(character =>
                !IsUpperAsciiLetterOrDigit(character)
                && !additionalCharacters.Contains(character, StringComparison.Ordinal)))
        {
            throw DomainGuard.Validation(
                parameterName,
                $"{parameterName} contains unsupported characters.");
        }

        return normalized;
    }

    public static string Hash(string? value, string parameterName)
    {
        string normalized = Required(value, 64, parameterName).ToLowerInvariant();
        if (normalized.Length != 64 || normalized.Any(character => !IsLowerHex(character)))
        {
            throw DomainGuard.Validation(
                parameterName,
                $"{parameterName} must be a 64-character SHA-256 hexadecimal value.");
        }

        return normalized;
    }

    private static bool IsUpperAsciiLetterOrDigit(char value)
    {
        return value is >= 'A' and <= 'Z' or >= '0' and <= '9';
    }

    private static bool IsLowerHex(char value)
    {
        return value is >= '0' and <= '9' or >= 'a' and <= 'f';
    }
}
