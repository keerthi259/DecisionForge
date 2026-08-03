using System.Reflection;
using DecisionForge.Application;
using DecisionForge.Application.Reliability.Notifications;
using DecisionForge.Application.Reliability.Outbox;
using DecisionForge.Domain.Audit;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Notifications;
using DecisionForge.Domain.Outbox;

namespace DecisionForge.ArchitectureTests;

public sealed class ReliabilityArchitectureTests
{
    [Fact]
    public void ReliabilityPortsAreSpecificNonGenericAndCancellationAware()
    {
        Type[] ports = [typeof(IOutboxStore), typeof(INotificationStore), typeof(INotificationSender)];

        Assert.All(ports, port =>
        {
            Assert.True(port.IsInterface);
            Assert.False(port.IsGenericType);
        });
        Assert.DoesNotContain(
            ports.SelectMany(port => port.GetMethods()),
            method => method.GetParameters().Last().ParameterType != typeof(CancellationToken));
    }

    [Fact]
    public void ReliabilityDomainTypesAreClosedAndExposeNoMutableSetters()
    {
        Type[] types = [typeof(AuditEvent), typeof(AuditPayload), typeof(OutboxMessage), typeof(Notification)];

        Assert.All(types, type =>
        {
            Assert.True(type.IsSealed);
            Assert.DoesNotContain(
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                property => property.SetMethod?.IsPublic == true);
        });
        Assert.True(typeof(Entity).IsAssignableFrom(typeof(AuditEvent)));
        Assert.True(typeof(Entity).IsAssignableFrom(typeof(OutboxMessage)));
        Assert.True(typeof(Entity).IsAssignableFrom(typeof(Notification)));
    }

    [Fact]
    public void ApplicationReliabilityHasNoPersistenceOrHostingDependency()
    {
        Assembly application = typeof(ApplicationAssembly).Assembly;
        string[] forbidden = application.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name is "Npgsql" or "Microsoft.Extensions.Hosting.Abstractions")
            .ToArray();

        Assert.Empty(forbidden);
        Assert.DoesNotContain(
            application.GetTypes(),
            type => type.IsGenericTypeDefinition
                && type.Name.Contains("Repository", StringComparison.Ordinal));
    }

    [Fact]
    public void OutboxDispatcherHasBoundedDependenciesAndBatchLimit()
    {
        ConstructorInfo constructor = Assert.Single(typeof(OutboxDispatcher).GetConstructors());

        Assert.Equal(4, constructor.GetParameters().Length);
        Assert.Throws<InvalidOperationException>(() => new OutboxDispatcherOptions
        {
            BatchSize = 101,
        }.Validate());
    }
}
