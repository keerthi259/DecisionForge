using DecisionForge.Api.Exports;
using DecisionForge.Api.Foundation;
using DecisionForge.Api.Foundation.Idempotency;
using DecisionForge.Application;

namespace DecisionForge.ArchitectureTests;

public sealed class ApiFoundationArchitectureTests
{
    [Fact]
    public void CrossCuttingHttpTypesStayInApiLayer()
    {
        Type[] types =
        [
            typeof(ApiExceptionHandler),
            typeof(ApiListQueryParser),
            typeof(EntityTagSupport),
            typeof(IdempotencyMiddleware),
            typeof(SafeCsv),
        ];

        Assert.All(types, type => Assert.StartsWith(
            "DecisionForge.Api.",
            type.Namespace,
            StringComparison.Ordinal));
        Assert.DoesNotContain(
            typeof(ApplicationAssembly).Assembly.GetReferencedAssemblies(),
            reference => string.Equals(
                reference.Name,
                "Microsoft.AspNetCore.OpenApi",
                StringComparison.Ordinal));
    }

    [Fact]
    public void ApiFoundationMiddlewareAndOptionsAreClosedForExtension()
    {
        Type[] types =
        [
            typeof(ApiExceptionHandler),
            typeof(ApiFoundationOptions),
            typeof(IdempotencyMiddleware),
            typeof(RequestBodyLimitMiddleware),
            typeof(SecurityHeadersMiddleware),
        ];

        Assert.All(types, type => Assert.True(type.IsSealed, $"{type.Name} must be sealed."));
    }

    [Fact]
    public void IdempotencyStoreContractPropagatesCancellation()
    {
        System.Reflection.MethodInfo[] operations = typeof(IApiIdempotencyStore).GetMethods();

        Assert.Equal(3, operations.Length);
        Assert.All(operations, operation => Assert.Equal(
            typeof(CancellationToken),
            operation.GetParameters()[^1].ParameterType));
    }
}
