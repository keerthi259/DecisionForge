using System.Reflection;
using System.Runtime.CompilerServices;
using DecisionForge.Application;
using DecisionForge.Application.Approvals;
using DecisionForge.Application.Approvals.Ports;
using DecisionForge.Domain.Approvals;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.PurchaseRequests;

namespace DecisionForge.ArchitectureTests;

public sealed class ApprovalWorkflowArchitectureTests
{
    private static readonly Assembly _applicationAssembly = typeof(ApplicationAssembly).Assembly;

    [Fact]
    public void ApprovalPortsAreSpecificNonGenericAndCancellationAware()
    {
        Type[] ports =
        [
            typeof(IApprovalActionTransaction),
            typeof(IApprovalAuthorization),
            typeof(IApprovalQueries),
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
        Assert.DoesNotContain(
            _applicationAssembly.GetTypes(),
            type => type.IsGenericTypeDefinition
                && type.Name.Contains("Repository", StringComparison.Ordinal));
    }

    [Fact]
    public void ApprovalActionCommitContainsWorkflowAndRequestInOneBoundary()
    {
        MethodInfo method = typeof(IApprovalActionTransaction)
            .GetMethod(nameof(IApprovalActionTransaction.CommitAsync))!;
        Type[] parameters = method.GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.Equal(typeof(ApprovalWorkflow), parameters[0]);
        Assert.Equal(typeof(PurchaseRequest), parameters[1]);
        Assert.Equal(typeof(CancellationToken), parameters[2]);
    }

    [Fact]
    public void ApprovalCommandsCannotSupplyActorOrApproverRole()
    {
        Type[] commands =
        [
            typeof(ApproveApprovalStageCommand),
            typeof(RejectApprovalStageCommand),
            typeof(OverrideApprovalWorkflowCommand),
        ];

        Assert.All(commands, command =>
        {
            string[] propertyNames = command.GetProperties()
                .Select(property => property.Name)
                .ToArray();
            Assert.DoesNotContain("ActorId", propertyNames);
            Assert.DoesNotContain("UserId", propertyNames);
            Assert.DoesNotContain("RequiredRole", propertyNames);
        });
    }

    [Fact]
    public void ApprovalDomainAndProjectionTypesAreClosedAndImmutable()
    {
        Type[] types =
        [
            typeof(ApprovalWorkflow),
            typeof(ApprovalStage),
            typeof(ApprovalOverride),
            typeof(ApprovalWorkflowDetail),
            typeof(ApprovalStageDetail),
            typeof(ApprovalInboxItem),
        ];

        Assert.All(types, type =>
        {
            Assert.True(type.IsSealed);
            Assert.DoesNotContain(
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                property => property.SetMethod?.IsPublic == true
                    && !property.SetMethod.ReturnParameter
                        .GetRequiredCustomModifiers()
                        .Contains(typeof(IsExternalInit)));
        });
        Assert.Empty(typeof(ApprovalWorkflow).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(ApprovalStage).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.True(typeof(AggregateRoot).IsAssignableFrom(typeof(ApprovalWorkflow)));
        Assert.True(typeof(Entity).IsAssignableFrom(typeof(ApprovalStage)));
    }

    [Fact]
    public void ApprovalServicesHaveBoundedDependenciesAndQueriesAreBounded()
    {
        Type[] services = [typeof(ApprovalWorkflowService), typeof(ApprovalQueryService)];
        Assert.All(services, service =>
        {
            ConstructorInfo constructor = Assert.Single(service.GetConstructors());
            Assert.InRange(constructor.GetParameters().Length, 1, 6);
        });
        Assert.Equal(100, ApprovalInboxPage.MaximumPageSize);
        MethodInfo list = typeof(IApprovalQueries)
            .GetMethod(nameof(IApprovalQueries.ListForAuthorizedRolesAsync))!;
        Assert.Contains(
            list.GetParameters(),
            parameter => parameter.ParameterType == typeof(ApprovalInboxPage));
    }
}
