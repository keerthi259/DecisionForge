using System.Collections.ObjectModel;
using DecisionForge.Domain.Enums;

namespace DecisionForge.Domain.Policies.Facts;

public static class PolicyFactRegistry
{
    private static readonly PolicyOperator[] _equality =
    [
        PolicyOperator.Equals,
        PolicyOperator.NotEquals,
        PolicyOperator.In,
        PolicyOperator.NotIn,
        PolicyOperator.Exists,
        PolicyOperator.NotExists,
    ];

    private static readonly PolicyOperator[] _numeric =
    [
        .. _equality,
        PolicyOperator.GreaterThan,
        PolicyOperator.GreaterThanOrEqual,
        PolicyOperator.LessThan,
        PolicyOperator.LessThanOrEqual,
    ];

    private static readonly PolicyOperator[] _string =
    [
        .. _equality,
        PolicyOperator.Contains,
    ];

    private static readonly PolicyOperator[] _boolean =
    [
        PolicyOperator.Equals,
        PolicyOperator.NotEquals,
        PolicyOperator.Exists,
        PolicyOperator.NotExists,
    ];

    public static IReadOnlyDictionary<string, PolicyFactMetadata> All { get; } = CreateFacts();

    public static bool TryGet(string path, out PolicyFactMetadata metadata)
    {
        return All.TryGetValue(path, out metadata!);
    }

    private static ReadOnlyDictionary<string, PolicyFactMetadata> CreateFacts()
    {
        PolicyFactMetadata[] facts =
        [
            Fact("request.totalAmount", PolicyFactValueType.DecimalNumber, _numeric),
            Fact("request.currency", PolicyFactValueType.Text, _string),
            EnumFact<ProcurementCategory>("request.category"),
            EnumFact<Urgency>("request.urgency"),
            EnumFact<DataSensitivity>("request.dataSensitivity"),
            Fact("request.itemCount", PolicyFactValueType.WholeNumber, _numeric),
            Fact("request.expectedDeliveryDays", PolicyFactValueType.WholeNumber, _numeric),
            Fact("request.hasBusinessJustification", PolicyFactValueType.Logical, _boolean),
            Fact("department.code", PolicyFactValueType.Text, _string),
            Fact("department.autoApprovalLimit", PolicyFactValueType.DecimalNumber, _numeric),
            Fact("supplier.isApproved", PolicyFactValueType.Logical, _boolean),
            EnumFact<SupplierOnboardingStatus>("supplier.onboardingStatus"),
            EnumFact<SupplierRiskRating>("supplier.riskRating"),
            Fact("supplier.isActive", PolicyFactValueType.Logical, _boolean),
            Fact("derived.containsTechnologyPurchase", PolicyFactValueType.Logical, _boolean),
            Fact("derived.requiresUrgencyException", PolicyFactValueType.Logical, _boolean),
        ];

        return new ReadOnlyDictionary<string, PolicyFactMetadata>(
            facts.ToDictionary(fact => fact.Path, StringComparer.Ordinal));
    }

    private static PolicyFactMetadata Fact(
        string path,
        PolicyFactValueType type,
        IEnumerable<PolicyOperator> operators)
    {
        return new PolicyFactMetadata(path, type, operators);
    }

    private static PolicyFactMetadata EnumFact<TEnum>(string path)
        where TEnum : struct, Enum
    {
        return new PolicyFactMetadata(
            path,
            PolicyFactValueType.ControlledText,
            _equality,
            Enum.GetNames<TEnum>());
    }
}
