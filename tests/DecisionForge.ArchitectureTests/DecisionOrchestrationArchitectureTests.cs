using System.Reflection;
using DecisionForge.Application;
using DecisionForge.Application.Decisions;
using DecisionForge.Application.Decisions.Ports;
using DecisionForge.Application.PurchaseRequests.Idempotency;
using DecisionForge.Domain.Approvals;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Decisions;
using DecisionForge.Domain.PurchaseRequests;

namespace DecisionForge.ArchitectureTests;

public sealed class DecisionOrchestrationArchitectureTests
{
    private static readonly Assembly _applicationAssembly = typeof(ApplicationAssembly).Assembly;

    [Fact]
    public void DecisionPersistenceAndEvaluationPortsAreSpecificAndCancellationAware()
    {
        Type[] ports =
        [
            typeof(IPolicyDecisionQueries),
            typeof(IPolicyEvaluationEngine),
            typeof(IDecisionRepository),
            typeof(IDecisionTransaction),
        ];

        Assert.All(ports, port =>
        {
            Assert.True(port.IsInterface);
            Assert.False(port.IsGenericType);
        });
        string[] missingCancellation = ports
            .SelectMany(port => port.GetMethods())
            .Where(method => method.GetParameters().LastOrDefault()?.ParameterType
                != typeof(CancellationToken))
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}")
            .ToArray();
        Assert.Empty(missingCancellation);
    }

    [Fact]
    public void AtomicDecisionCommitContainsRequestDecisionAndIdempotencyEvidence()
    {
        MethodInfo commit = typeof(IDecisionTransaction)
            .GetMethod(nameof(IDecisionTransaction.CommitDecisionAsync))!;
        Type[] parameters = commit.GetParameters().Select(parameter => parameter.ParameterType).ToArray();

        Assert.Equal(typeof(PurchaseRequest), parameters[0]);
        Assert.Equal(typeof(Decision), parameters[1]);
        Assert.Equal(typeof(ApprovalWorkflow), parameters[2]);
        Assert.Equal(typeof(PurchaseRequestSubmissionRecord), parameters[3]);
        Assert.Equal(typeof(CancellationToken), parameters[4]);
        Assert.DoesNotContain(
            typeof(IDecisionRepository).GetMethods(),
            method => method.Name.StartsWith("Add", StringComparison.Ordinal));
    }

    [Fact]
    public void DecisionEntitiesAndEvidenceResultsAreClosedAndImmutable()
    {
        Type[] types =
        [
            typeof(Decision),
            typeof(RuleEvaluation),
            typeof(EvaluationPolicyReference),
            typeof(PurchaseRequestEvaluationContext),
            typeof(DecisionExplanation),
            typeof(DecisionReproductionComparison),
        ];

        Assert.All(types, type =>
        {
            Assert.True(type.IsSealed);
            Assert.DoesNotContain(
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                property => property.SetMethod?.IsPublic == true);
        });
        Assert.Empty(typeof(Decision).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(RuleEvaluation).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void OrchestrationServicesHaveBoundedDependenciesAndNoFrameworkDependency()
    {
        Type[] services =
        [
            typeof(DecisionSubmissionService),
            typeof(DecisionEvaluationCoordinator),
            typeof(DecisionEvidenceService),
            typeof(DecisionSubmissionPersistence),
        ];

        Assert.All(services, service =>
        {
            ConstructorInfo constructor = Assert.Single(service.GetConstructors());
            Assert.InRange(constructor.GetParameters().Length, 1, 6);
        });
        string[] forbidden = _applicationAssembly.GetReferencedAssemblies()
            .Select(assembly => assembly.Name!)
            .Where(name => name is "Microsoft.EntityFrameworkCore" or "Npgsql")
            .ToArray();
        Assert.Empty(forbidden);
    }

    [Fact]
    public void DecisionQueriesRequireTrustedOwnerScope()
    {
        MethodInfo method = typeof(IDecisionRepository)
            .GetMethod(nameof(IDecisionRepository.FindOwnedByPurchaseRequestIdAsync))!;
        ParameterInfo[] parameters = method.GetParameters();

        Assert.Equal("purchaseRequestId", parameters[0].Name);
        Assert.Equal("requesterId", parameters[1].Name);
        Assert.Equal(typeof(Guid), parameters[0].ParameterType);
        Assert.Equal(typeof(Guid), parameters[1].ParameterType);
    }

    [Fact]
    public void NoGenericDecisionRepositoryWasIntroduced()
    {
        string[] genericRepositories = _applicationAssembly.GetTypes()
            .Where(type => type.Namespace?.Contains("Decisions", StringComparison.Ordinal) == true)
            .Where(type => type.Name.Contains("Repository", StringComparison.Ordinal))
            .Where(type => type.IsGenericTypeDefinition)
            .Select(type => type.FullName!)
            .ToArray();

        Assert.Empty(genericRepositories);
        Assert.True(typeof(AggregateRoot).IsAssignableFrom(typeof(Decision)));
        Assert.True(typeof(Entity).IsAssignableFrom(typeof(RuleEvaluation)));
    }
}
