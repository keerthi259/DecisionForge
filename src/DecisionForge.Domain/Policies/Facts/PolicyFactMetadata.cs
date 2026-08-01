using System.Collections.ObjectModel;

namespace DecisionForge.Domain.Policies.Facts;

public sealed record PolicyFactMetadata
{
    internal PolicyFactMetadata(
        string path,
        PolicyFactValueType valueType,
        IEnumerable<PolicyOperator> allowedOperators,
        IEnumerable<string>? allowedValues = null)
    {
        Path = path;
        ValueType = valueType;
        AllowedOperators = new ReadOnlyCollection<PolicyOperator>(allowedOperators.ToArray());
        AllowedValues = new ReadOnlyCollection<string>((allowedValues ?? []).ToArray());
    }

    public string Path { get; }

    public PolicyFactValueType ValueType { get; }

    public IReadOnlyList<PolicyOperator> AllowedOperators { get; }

    public IReadOnlyList<string> AllowedValues { get; }
}
