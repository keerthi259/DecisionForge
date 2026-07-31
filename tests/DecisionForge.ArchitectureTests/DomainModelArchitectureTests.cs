using System.Reflection;
using System.Runtime.CompilerServices;
using DecisionForge.Domain;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.PurchaseRequests;
using DecisionForge.Domain.ReferenceData;
using NetArchTest.Rules;

namespace DecisionForge.ArchitectureTests;

public sealed class DomainModelArchitectureTests
{
    private static readonly Assembly _domainAssembly = typeof(DomainAssembly).Assembly;

    [Fact]
    public void DomainDoesNotDependOnFrameworkOrPersistenceNamespaces()
    {
        TestResult result = Types.InAssembly(_domainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.AspNetCore",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.Extensions",
                "Npgsql")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public void DomainModelDoesNotExposePublicPropertySetters()
    {
        IReadOnlyList<string> violations = _domainAssembly.GetTypes()
            .Where(type => typeof(Entity).IsAssignableFrom(type))
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(property => property.SetMethod?.IsPublic == true)
            .Select(property => $"{property.DeclaringType!.FullName}.{property.Name}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void EveryConcreteDomainEventImplementsDomainEventContract()
    {
        Type[] eventTypes = _domainAssembly.GetTypes()
            .Where(type => type.Name.EndsWith("DomainEvent", StringComparison.Ordinal))
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .ToArray();

        Assert.NotEmpty(eventTypes);
        Assert.All(eventTypes, type => Assert.True(typeof(IDomainEvent).IsAssignableFrom(type)));
    }

    [Fact]
    public void AggregateAndOwnedEntityAreSealed()
    {
        Assert.True(typeof(PurchaseRequest).IsSealed);
        Assert.True(typeof(PurchaseRequestItem).IsSealed);
        Assert.True(typeof(Department).IsSealed);
        Assert.True(typeof(Supplier).IsSealed);
    }

    [Fact]
    public void DomainDoesNotGrantTestsInternalAccess()
    {
        IEnumerable<string> friendAssemblies = _domainAssembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName);

        Assert.DoesNotContain(
            friendAssemblies,
            name => name.StartsWith("DecisionForge.", StringComparison.Ordinal));
    }
}
