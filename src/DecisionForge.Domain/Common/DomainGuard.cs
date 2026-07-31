namespace DecisionForge.Domain.Common;

internal static class DomainGuard
{
    public static Guid NotEmpty(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw Validation(parameterName, $"{parameterName} must not be empty.");
        }

        return value;
    }

    public static DateTimeOffset Utc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw Validation(parameterName, $"{parameterName} must use the UTC offset.");
        }

        return value;
    }

    public static DomainRuleException Validation(string parameterName, string message)
    {
        return new DomainRuleException(DomainErrorCodes.Validation, message, parameterName);
    }
}
