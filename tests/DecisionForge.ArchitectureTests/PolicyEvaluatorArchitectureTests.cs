using System.Reflection;
using DecisionForge.Domain;
using DecisionForge.Domain.Policies.Evaluation;

namespace DecisionForge.ArchitectureTests;

public sealed class PolicyEvaluatorArchitectureTests
{
    private static readonly Assembly _domainAssembly = typeof(DomainAssembly).Assembly;

    [Fact]
    public void EvaluationContractsAreSealedAndPubliclyImmutable()
    {
        Type[] contracts =
        [
            typeof(PolicyFact),
            typeof(PolicyFactSet),
            typeof(PolicyFactAccess),
            typeof(PolicyConditionEvaluation),
            typeof(PolicyRuleEvaluation),
            typeof(PolicyEvaluationReason),
            typeof(PolicyEvaluationResult),
        ];

        Assert.All(contracts, type =>
        {
            Assert.True(type.IsSealed);
            Assert.DoesNotContain(
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                property => property.SetMethod?.IsPublic == true);
        });
    }

    [Fact]
    public void EvaluatorIsSynchronousStatelessAndCancellationAware()
    {
        Assert.True(typeof(PolicyEvaluator).IsAbstract && typeof(PolicyEvaluator).IsSealed);
        MethodInfo evaluate = Assert.Single(typeof(PolicyEvaluator).GetMethods(
            BindingFlags.Public | BindingFlags.Static));
        ParameterInfo[] parameters = evaluate.GetParameters();

        Assert.Equal(typeof(PolicyEvaluationResult), evaluate.ReturnType);
        Assert.Equal(typeof(CancellationToken), parameters[^1].ParameterType);
        Assert.True(parameters[^1].HasDefaultValue);
    }

    [Fact]
    public void PolicyEngineHasNoIoOrExecutableExpressionDependency()
    {
        string[] forbiddenNamespacePrefixes =
        [
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore",
            "Npgsql",
            "System.Data",
            "System.Net",
            "System.Reflection.Emit",
            "Microsoft.CodeAnalysis",
        ];
        Type[] engineTypes = _domainAssembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith(
                "DecisionForge.Domain.Policies.Evaluation",
                StringComparison.Ordinal) == true)
            .ToArray();

        Assert.NotEmpty(engineTypes);
        Assert.All(engineTypes, type => Assert.DoesNotContain(
            forbiddenNamespacePrefixes,
            prefix => type.Namespace?.StartsWith(prefix, StringComparison.Ordinal) == true));
        Assert.DoesNotContain(
            _domainAssembly.GetReferencedAssemblies(),
            reference => forbiddenNamespacePrefixes.Any(prefix =>
                reference.Name?.StartsWith(prefix, StringComparison.Ordinal) == true));
    }
}
