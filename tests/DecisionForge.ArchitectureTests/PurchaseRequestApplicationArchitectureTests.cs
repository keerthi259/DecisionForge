using System.Reflection;
using DecisionForge.Application;
using DecisionForge.Application.Decisions;
using DecisionForge.Application.PurchaseRequests;
using DecisionForge.Application.PurchaseRequests.Idempotency;
using DecisionForge.Application.PurchaseRequests.Ports;
using DecisionForge.Application.PurchaseRequests.Submission;

namespace DecisionForge.ArchitectureTests;

public sealed class PurchaseRequestApplicationArchitectureTests
{
    private static readonly Assembly _applicationAssembly = typeof(ApplicationAssembly).Assembly;

    [Fact]
    public void RequestPersistenceUsesSpecificNonGenericPorts()
    {
        Type[] ports =
        [
            typeof(IPurchaseRequestRepository),
            typeof(IPurchaseRequestQueries),
            typeof(IPurchaseRequestNumberGenerator),
            typeof(IPurchaseRequestSubmissionIdempotencyStore),
        ];

        Assert.All(ports, port =>
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
    public void EveryRequestPortOperationRequiresCancellationToken()
    {
        Type[] ports =
        [
            typeof(IPurchaseRequestRepository),
            typeof(IPurchaseRequestQueries),
            typeof(IPurchaseRequestNumberGenerator),
            typeof(IPurchaseRequestSubmissionIdempotencyStore),
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
    public void MutatingRequestContractsCannotAcceptOwnershipOrClientTotals()
    {
        Type[] commands =
        [
            typeof(CreatePurchaseRequestCommand),
            typeof(UpdatePurchaseRequestDraftCommand),
            typeof(AddPurchaseRequestItemCommand),
            typeof(UpdatePurchaseRequestItemCommand),
            typeof(RemovePurchaseRequestItemCommand),
            typeof(WithdrawPurchaseRequestCommand),
            typeof(ClonePurchaseRequestCommand),
            typeof(SubmitPurchaseRequestForDecisionCommand),
            typeof(RetryPurchaseRequestEvaluationCommand),
        ];
        string[] forbiddenNames = ["RequesterId", "OwnerId", "Total", "LineTotal"];

        string[] violations = commands
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(property => forbiddenNames.Contains(property.Name, StringComparer.Ordinal))
            .Select(property => $"{property.DeclaringType!.Name}.{property.Name}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void RequestQueryContractsAreBoundedAndImmutable()
    {
        Assert.Equal(100, PurchaseRequestPage.MaximumPageSize);
        Type[] resultTypes =
        [
            typeof(PurchaseRequestSummary),
            typeof(PurchaseRequestItemDetail),
            typeof(PurchaseRequestDetail),
            typeof(PurchaseRequestListResult),
            typeof(PurchaseRequestSubmissionRecord),
            typeof(SubmissionIdempotencyResolution),
            typeof(SubmissionPreconditionError),
            typeof(SubmissionPreconditionResult),
        ];

        string[] mutableProperties = resultTypes
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(property => property.SetMethod?.IsPublic == true)
            .Select(property => $"{property.DeclaringType!.Name}.{property.Name}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(mutableProperties);
    }

    [Fact]
    public void RequestRepositoryLoadsOnlyWithinTrustedOwnerScope()
    {
        MethodInfo method = typeof(IPurchaseRequestRepository)
            .GetMethod(nameof(IPurchaseRequestRepository.FindOwnedByIdAsync))!;
        ParameterInfo[] parameters = method.GetParameters();

        Assert.Equal(typeof(Guid), parameters[0].ParameterType);
        Assert.Equal("purchaseRequestId", parameters[0].Name);
        Assert.Equal(typeof(Guid), parameters[1].ParameterType);
        Assert.Equal("requesterId", parameters[1].Name);
    }
}
