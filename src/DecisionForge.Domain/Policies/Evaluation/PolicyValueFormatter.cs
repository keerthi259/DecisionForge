using System.Globalization;
using DecisionForge.Domain.Policies.Contracts;

namespace DecisionForge.Domain.Policies.Evaluation;

internal static class PolicyValueFormatter
{
    public static string Format(PolicyValue value)
    {
        return value switch
        {
            PolicyStringValue text => text.Value,
            PolicyNumberValue number => number.Value.ToString(
                "G29",
                CultureInfo.InvariantCulture),
            PolicyBooleanValue boolean => boolean.Value ? "true" : "false",
            _ => throw Unsupported(value),
        };
    }

    private static PolicyEvaluationException Unsupported(PolicyValue value)
    {
        return new PolicyEvaluationException(PolicyEvaluationErrorCodes.FactTypeMismatch, "$", $"The evaluation value type '{value.GetType().Name}' is unsupported.");
    }
}
