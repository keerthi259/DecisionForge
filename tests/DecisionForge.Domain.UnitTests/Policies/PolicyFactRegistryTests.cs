using DecisionForge.Domain.Policies;
using DecisionForge.Domain.Policies.Facts;

namespace DecisionForge.Domain.UnitTests.Policies;

public sealed class PolicyFactRegistryTests
{
    private static readonly string[] _approvedPaths =
    [
        "department.autoApprovalLimit",
        "department.code",
        "derived.containsTechnologyPurchase",
        "derived.requiresUrgencyException",
        "request.category",
        "request.currency",
        "request.dataSensitivity",
        "request.expectedDeliveryDays",
        "request.hasBusinessJustification",
        "request.itemCount",
        "request.totalAmount",
        "request.urgency",
        "supplier.isActive",
        "supplier.isApproved",
        "supplier.onboardingStatus",
        "supplier.riskRating",
    ];

    [Fact]
    public void RegistryContainsExactlyEveryApprovedFactPath()
    {
        Assert.Equal(
            _approvedPaths,
            PolicyFactRegistry.All.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(16, PolicyFactRegistry.All.Count);
        Assert.False(PolicyFactRegistry.TryGet("request.internalSecret", out _));
    }

    [Fact]
    public void RegistryAssignsTypesAndAllowedOperators()
    {
        AssertMetadata(
            "request.totalAmount",
            PolicyFactValueType.DecimalNumber,
            PolicyOperator.GreaterThan,
            allowed: true);
        AssertMetadata(
            "request.itemCount",
            PolicyFactValueType.WholeNumber,
            PolicyOperator.GreaterThanOrEqual,
            allowed: true);
        AssertMetadata(
            "request.currency",
            PolicyFactValueType.Text,
            PolicyOperator.Contains,
            allowed: true);
        AssertMetadata(
            "request.category",
            PolicyFactValueType.ControlledText,
            PolicyOperator.Contains,
            allowed: false);
        AssertMetadata(
            "supplier.isActive",
            PolicyFactValueType.Logical,
            PolicyOperator.GreaterThan,
            allowed: false);
    }

    [Fact]
    public void ControlledFactsExposeExactEnumNames()
    {
        PolicyFactMetadata category = PolicyFactRegistry.All["request.category"];
        PolicyFactMetadata onboarding = PolicyFactRegistry.All["supplier.onboardingStatus"];

        Assert.Contains("Hardware", category.AllowedValues);
        Assert.Contains("Other", category.AllowedValues);
        Assert.Contains("Suspended", onboarding.AllowedValues);
        Assert.DoesNotContain("suspended", onboarding.AllowedValues);
    }

    [Fact]
    public void OperatorJsonNamesAreExactAndCaseSensitive()
    {
        Assert.Equal(11, PolicyOperatorNames.All.Count);
        foreach ((string name, PolicyOperator value) in PolicyOperatorNames.All)
        {
            Assert.Equal(name, PolicyOperatorNames.ToJsonName(value));
            Assert.True(PolicyOperatorNames.TryParse(name, out PolicyOperator parsedValue));
            Assert.Equal(value, parsedValue);
        }

        Assert.True(PolicyOperatorNames.TryParse("greaterThanOrEqual", out PolicyOperator parsed));
        Assert.Equal(PolicyOperator.GreaterThanOrEqual, parsed);
        Assert.Equal("greaterThanOrEqual", PolicyOperatorNames.ToJsonName(parsed));
        Assert.False(PolicyOperatorNames.TryParse("GreaterThanOrEqual", out _));
        Assert.False(PolicyOperatorNames.TryParse(null, out _));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PolicyOperatorNames.ToJsonName((PolicyOperator)999));
    }

    private static void AssertMetadata(
        string path,
        PolicyFactValueType expectedType,
        PolicyOperator @operator,
        bool allowed)
    {
        Assert.True(PolicyFactRegistry.TryGet(path, out PolicyFactMetadata metadata));
        Assert.Equal(expectedType, metadata.ValueType);
        Assert.Equal(allowed, metadata.AllowedOperators.Contains(@operator));
    }
}
