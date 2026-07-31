namespace DecisionForge.Domain.Common;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
