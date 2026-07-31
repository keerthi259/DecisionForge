namespace DecisionForge.Application.Platform;

public interface ICorrelationContextAccessor
{
    string? CorrelationId { get; }

    IDisposable Push(string correlationId);
}
