using System.Reflection;
using DecisionForge.Application;
using DecisionForge.Application.ReferenceData.Ports;
using DecisionForge.Domain.EvaluationFacts;

namespace DecisionForge.ArchitectureTests;

public sealed class ReferenceDataArchitectureTests
{
    private static readonly Assembly _applicationAssembly = typeof(ApplicationAssembly).Assembly;

    [Fact]
    public void ReferenceDataUsesSpecificNonGenericRepositoryAndQueryPorts()
    {
        Type[] expectedPorts =
        [
            typeof(IDepartmentRepository),
            typeof(IDepartmentQueries),
            typeof(ISupplierRepository),
            typeof(ISupplierQueries),
        ];

        Assert.All(expectedPorts, port =>
        {
            Assert.True(port.IsInterface);
            Assert.False(port.IsGenericType);
        });

        string[] genericRepositories = _applicationAssembly.GetTypes()
            .Where(type => type.Name.Contains("Repository", StringComparison.Ordinal))
            .Where(type => type.IsGenericTypeDefinition)
            .Select(type => type.FullName!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Empty(genericRepositories);
    }

    [Fact]
    public void EveryReferenceDataPortOperationRequiresCancellationToken()
    {
        Type[] ports =
        [
            typeof(IDepartmentRepository),
            typeof(IDepartmentQueries),
            typeof(ISupplierRepository),
            typeof(ISupplierQueries),
        ];

        string[] violations = ports
            .SelectMany(port => port.GetMethods())
            .Where(method => method.GetParameters().LastOrDefault()?.ParameterType
                != typeof(CancellationToken))
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void EvaluationSnapshotExposesOnlyApprovedFactPaths()
    {
        Assert.Equal(
            ["Department", "Derived", "Request", "Supplier"],
            PublicPropertyNames(typeof(EvaluationFactSnapshot)));
        Assert.Equal(
            [
                "Category",
                "Currency",
                "DataSensitivity",
                "ExpectedDeliveryDays",
                "HasBusinessJustification",
                "ItemCount",
                "TotalAmount",
                "Urgency",
            ],
            PublicPropertyNames(typeof(RequestEvaluationFacts)));
        Assert.Equal(
            ["AutoApprovalLimit", "Code"],
            PublicPropertyNames(typeof(DepartmentEvaluationFacts)));
        Assert.Equal(
            ["IsActive", "IsApproved", "OnboardingStatus", "RiskRating"],
            PublicPropertyNames(typeof(SupplierEvaluationFacts)));
        Assert.Equal(
            ["ContainsTechnologyPurchase", "RequiresUrgencyException"],
            PublicPropertyNames(typeof(DerivedEvaluationFacts)));
    }

    [Fact]
    public void EvaluationFactsCannotBeConstructedOrMutatedByPolicyConsumers()
    {
        Type[] factTypes =
        [
            typeof(EvaluationFactSnapshot),
            typeof(RequestEvaluationFacts),
            typeof(DepartmentEvaluationFacts),
            typeof(SupplierEvaluationFacts),
            typeof(DerivedEvaluationFacts),
        ];

        Assert.All(factTypes, type =>
        {
            Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
            Assert.DoesNotContain(
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                property => property.SetMethod?.IsPublic == true);
        });
    }

    private static string[] PublicPropertyNames(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
