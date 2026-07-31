using System.Collections.ObjectModel;

namespace DecisionForge.Domain.Common;

public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];
    private readonly ReadOnlyCollection<IDomainEvent> _domainEventsView;

    protected AggregateRoot(Guid id)
        : base(id)
    {
        _domainEventsView = _domainEvents.AsReadOnly();
    }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEventsView;

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    protected void Raise(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }
}
