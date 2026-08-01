using DecisionForge.Domain.Policies.Contracts;
using DecisionForge.Domain.Policies.Facts;

namespace DecisionForge.Domain.Policies.Evaluation;

public sealed record PolicyFact
{
    private PolicyFact(
        string path,
        PolicyFactValueType valueType,
        PolicyValue value)
    {
        Path = path;
        ValueType = valueType;
        Value = value;
    }

    public string Path { get; }

    public PolicyFactValueType ValueType { get; }

    internal PolicyValue Value { get; }

    public static PolicyFact DecimalNumber(string path, decimal value)
    {
        return Create(path, PolicyFactValueType.DecimalNumber, new PolicyNumberValue(value));
    }

    public static PolicyFact Text(string path, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Create(path, PolicyFactValueType.Text, new PolicyStringValue(value));
    }

    public static PolicyFact ControlledText(string path, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        PolicyFact fact = Create(
            path,
            PolicyFactValueType.ControlledText,
            new PolicyStringValue(value));
        PolicyFactMetadata metadata = PolicyFactRegistry.All[path];
        if (!metadata.AllowedValues.Contains(value, StringComparer.Ordinal))
        {
            throw TypeMismatch(path);
        }

        return fact;
    }

    public static PolicyFact WholeNumber(string path, int value)
    {
        return Create(path, PolicyFactValueType.WholeNumber, new PolicyNumberValue(value));
    }

    public static PolicyFact Logical(string path, bool value)
    {
        return Create(path, PolicyFactValueType.Logical, new PolicyBooleanValue(value));
    }

    private static PolicyFact Create(
        string path,
        PolicyFactValueType expectedType,
        PolicyValue value)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!PolicyFactRegistry.TryGet(path, out PolicyFactMetadata metadata))
        {
            throw new PolicyEvaluationException(
                PolicyEvaluationErrorCodes.UnknownFact,
                path,
                "The evaluation input contains an unsupported fact path.");
        }

        if (metadata.ValueType != expectedType)
        {
            throw TypeMismatch(path);
        }

        return new PolicyFact(path, expectedType, value);
    }

    private static PolicyEvaluationException TypeMismatch(string path)
    {
        return new PolicyEvaluationException(
            PolicyEvaluationErrorCodes.FactTypeMismatch,
            path,
            "The evaluation fact value does not match its approved type.");
    }
}
