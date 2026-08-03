namespace DecisionForge.Application.Platform;

public interface ICurrentUserContext
{
    Guid? UserId { get; }
}
