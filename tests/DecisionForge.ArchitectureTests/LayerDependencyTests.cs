using DecisionForge.Api;
using DecisionForge.Application;
using DecisionForge.Domain;
using DecisionForge.Infrastructure;
using NetArchTest.Rules;

namespace DecisionForge.ArchitectureTests;

public sealed class LayerDependencyTests
{
    [Fact]
    public void DomainDoesNotDependOnOuterLayers()
    {
        TestResult result = Types.InAssembly(typeof(DomainAssembly).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                typeof(ApplicationAssembly).Namespace!,
                typeof(InfrastructureAssembly).Namespace!,
                typeof(ApiAssembly).Namespace!)
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void ApplicationDoesNotDependOnInfrastructureOrApi()
    {
        TestResult result = Types.InAssembly(typeof(ApplicationAssembly).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                typeof(InfrastructureAssembly).Namespace!,
                typeof(ApiAssembly).Namespace!)
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void InfrastructureDoesNotDependOnApi()
    {
        TestResult result = Types.InAssembly(typeof(InfrastructureAssembly).Assembly)
            .ShouldNot()
            .HaveDependencyOn(typeof(ApiAssembly).Namespace!)
            .GetResult();

        Assert.True(result.IsSuccessful);
    }
}
