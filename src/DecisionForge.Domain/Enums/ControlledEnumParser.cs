using DecisionForge.Domain.Common;

namespace DecisionForge.Domain.Enums;

public static class ControlledEnumParser
{
    public static TEnum Parse<TEnum>(string value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!TryParse(value, out TEnum result))
        {
            throw DomainGuard.Validation(
                parameterName,
                $"{parameterName} is not a supported {typeof(TEnum).Name} value.");
        }

        return result;
    }

    public static bool TryParse<TEnum>(string? value, out TEnum result)
        where TEnum : struct, Enum
    {
        if (value is null || !Enum.GetNames<TEnum>().Contains(value, StringComparer.Ordinal))
        {
            result = default;
            return false;
        }

        return Enum.TryParse(value, ignoreCase: false, out result);
    }
}
