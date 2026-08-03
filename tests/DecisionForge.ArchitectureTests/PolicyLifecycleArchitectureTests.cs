using System.Reflection;
using DecisionForge.Application;
using DecisionForge.Application.Policies;
using DecisionForge.Application.Policies.Auditing;
using DecisionForge.Application.Policies.Ports;
using DecisionForge.Domain.Policies.Lifecycle;

namespace DecisionForge.ArchitectureTests;

public sealed class PolicyLifecycleArchitectureTests
{
    private static readonly Assembly _applicationAssembly = typeof(ApplicationAssembly).Assembly;

    [Fact]
    public void PolicyLifecycleUsesSpecificNonGenericPorts()
    {
        Type[] ports = [typeof(IPolicyRepository), typeof(IPolicyQueries)];

        Assert.All(ports, port =>
        {
            Assert.True(port.IsInterface);
            Assert.False(port.IsGenericType);
        });
        string[] genericPolicyRepositories = _applicationAssembly.GetTypes()
            .Where(type => type.Namespace?.Contains("Policies", StringComparison.Ordinal) == true)
            .Where(type => type.Name.Contains("Repository", StringComparison.Ordinal))
            .Where(type => type.IsGenericTypeDefinition)
            .Select(type => type.FullName!)
            .ToArray();
        Assert.Empty(genericPolicyRepositories);
    }

    [Fact]
    public void EveryPolicyPortOperationRequiresCancellationToken()
    {
        Type[] ports = [typeof(IPolicyRepository), typeof(IPolicyQueries)];
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
    public void LifecycleEntitiesAndResultsAreClosedAndImmutable()
    {
        Type[] types =
        [
            typeof(Policy),
            typeof(PolicyVersion),
            typeof(PolicyVersionDiff),
            typeof(PolicyRuleModification),
            typeof(PolicyDraftValidationResult),
            typeof(PolicyLifecycleAuditRecord),
        ];

        Assert.All(types, type =>
        {
            Assert.True(type.IsSealed);
            Assert.DoesNotContain(
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                property => property.SetMethod?.IsPublic == true);
        });
        Assert.Empty(typeof(Policy).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(PolicyVersion).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void LifecycleServiceIsConcreteAndHasBoundedDependencies()
    {
        ConstructorInfo constructor = Assert.Single(typeof(PolicyLifecycleService).GetConstructors());

        Assert.True(typeof(PolicyLifecycleService).IsSealed);
        Assert.Equal(4, constructor.GetParameters().Length);
        Assert.Contains(
            constructor.GetParameters(),
            parameter => parameter.ParameterType == typeof(TimeProvider));
    }
}
