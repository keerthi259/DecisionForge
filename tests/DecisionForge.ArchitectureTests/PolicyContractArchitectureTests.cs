using System.Reflection;
using DecisionForge.Domain;
using DecisionForge.Domain.Policies.Conditions;
using DecisionForge.Domain.Policies.Contracts;
using DecisionForge.Domain.Policies.Facts;
using DecisionForge.Domain.Policies.Validation;

namespace DecisionForge.ArchitectureTests;

public sealed class PolicyContractArchitectureTests
{
    private static readonly Assembly _domainAssembly = typeof(DomainAssembly).Assembly;
    private static readonly string[] _factRoots =
    [
        "request",
        "department",
        "supplier",
        "derived",
    ];

    [Fact]
    public void PolicyConditionHierarchyIsClosedAndSealed()
    {
        Type[] concreteConditions = _domainAssembly.GetTypes()
            .Where(type => typeof(PolicyCondition).IsAssignableFrom(type))
            .Where(type => !type.IsAbstract)
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                typeof(PolicyAllCondition),
                typeof(PolicyAnyCondition),
                typeof(PolicyComparisonCondition),
                typeof(PolicyExistenceCondition),
                typeof(PolicyMembershipCondition),
                typeof(PolicyNotCondition),
            ],
            concreteConditions);
        Assert.All(concreteConditions, type => Assert.True(type.IsSealed));
    }

    [Fact]
    public void PolicyContractsCannotBePubliclyConstructedOrMutated()
    {
        Type[] contractTypes =
        [
            typeof(PolicyDefinition),
            typeof(PolicyRule),
            typeof(PolicyOutcome),
            typeof(PolicyStringValue),
            typeof(PolicyNumberValue),
            typeof(PolicyBooleanValue),
            typeof(PolicyAllCondition),
            typeof(PolicyAnyCondition),
            typeof(PolicyComparisonCondition),
            typeof(PolicyExistenceCondition),
            typeof(PolicyMembershipCondition),
            typeof(PolicyNotCondition),
            typeof(PolicyValidationError),
        ];

        Assert.All(contractTypes, type =>
        {
            Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
            Assert.DoesNotContain(
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                property => property.SetMethod?.IsPublic == true);
        });
    }

    [Fact]
    public void PolicyFactRegistryContainsOnlyApprovedPaths()
    {
        string[] paths = PolicyFactRegistry.All.Keys.Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(16, paths.Length);
        Assert.All(paths, path =>
            Assert.Contains(
                path.Split('.')[0],
                _factRoots));
    }

    [Fact]
    public void DomainDoesNotReferenceExecutablePolicyTechnologies()
    {
        string[] forbidden =
        [
            "Microsoft.CodeAnalysis",
            "Microsoft.CSharp",
            "System.CodeDom",
            "System.Data.DataSetExtensions",
        ];
        string[] references = _domainAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .ToArray();

        Assert.DoesNotContain(references, reference =>
            forbidden.Any(name => reference.StartsWith(name, StringComparison.Ordinal)));
    }
}
