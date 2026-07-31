using DecisionForge.Domain.Common;

namespace DecisionForge.Domain.UnitTests.Common;

public sealed class DomainPrimitiveTests
{
    [Fact]
    public void EntitiesUseTypeAndIdentifierForEquality()
    {
        Guid id = Guid.Parse("10000000-0000-7000-8000-000000000001");

        TestEntity first = new(id);
        TestEntity same = new(id);
        OtherTestEntity otherType = new(id);

        Assert.Equal(first, same);
        Assert.Equal(first.GetHashCode(), same.GetHashCode());
        Assert.False(first.Equals(otherType));
        Assert.False(first.Equals(null));
        Assert.False(first.Equals(new object()));
    }

    [Fact]
    public void EntityRejectsEmptyIdentifier()
    {
        DomainRuleException exception = Assert.Throws<DomainRuleException>(
            () => new TestEntity(Guid.Empty));

        Assert.Equal(DomainErrorCodes.Validation, exception.Code);
        Assert.Equal("id", exception.ParameterName);
    }

    [Fact]
    public void AggregateCollectsAndClearsEvents()
    {
        TestAggregate aggregate = new(Guid.Parse("10000000-0000-7000-8000-000000000002"));
        TestDomainEvent domainEvent = new(
            new DateTimeOffset(2026, 7, 31, 12, 0, 0, TimeSpan.Zero));

        aggregate.Record(domainEvent);

        Assert.Same(domainEvent, Assert.Single(aggregate.DomainEvents));
        ICollection<IDomainEvent> exposedEvents =
            Assert.IsAssignableFrom<ICollection<IDomainEvent>>(aggregate.DomainEvents);
        Assert.True(exposedEvents.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => exposedEvents.Add(domainEvent));
        aggregate.ClearDomainEvents();
        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void AggregateRejectsNullEvent()
    {
        TestAggregate aggregate = new(Guid.Parse("10000000-0000-7000-8000-000000000003"));

        Assert.Throws<ArgumentNullException>(() => aggregate.Record(null!));
    }

    [Fact]
    public void DomainRuleExceptionRequiresCodeAndExposesDetails()
    {
        DomainRuleException exception = new("stable-code", "Safe message", "field");

        Assert.Equal("stable-code", exception.Code);
        Assert.Equal("field", exception.ParameterName);
        Assert.Equal("Safe message", exception.Message);
        Assert.Throws<ArgumentException>(() => new DomainRuleException(" ", "message"));
    }

    private sealed class TestEntity : Entity
    {
        public TestEntity(Guid id)
            : base(id)
        {
        }
    }

    private sealed class OtherTestEntity : Entity
    {
        public OtherTestEntity(Guid id)
            : base(id)
        {
        }
    }

    private sealed class TestAggregate : AggregateRoot
    {
        public TestAggregate(Guid id)
            : base(id)
        {
        }

        public void Record(IDomainEvent domainEvent)
        {
            Raise(domainEvent);
        }
    }

    private sealed record TestDomainEvent(DateTimeOffset OccurredAt) : IDomainEvent;
}
