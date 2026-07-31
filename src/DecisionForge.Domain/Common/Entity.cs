namespace DecisionForge.Domain.Common;

public abstract class Entity : IEquatable<Entity>
{
    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new DomainRuleException(
                DomainErrorCodes.Validation,
                "Entity ID must not be empty.",
                nameof(id));
        }

        Id = id;
    }

    public Guid Id { get; }

    public bool Equals(Entity? other)
    {
        return other is not null && GetType() == other.GetType() && Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return obj is Entity other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(GetType(), Id);
    }
}
