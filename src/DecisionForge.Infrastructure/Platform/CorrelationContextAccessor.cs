using System.Threading;
using DecisionForge.Application.Platform;

namespace DecisionForge.Infrastructure.Platform;

public sealed class CorrelationContextAccessor : ICorrelationContextAccessor
{
    private readonly AsyncLocal<CorrelationScope?> _current = new();

    public string? CorrelationId => _current.Value?.CorrelationId;

    public IDisposable Push(string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        CorrelationScope scope = new(this, correlationId, _current.Value);
        _current.Value = scope;
        return scope;
    }

    private sealed class CorrelationScope(
        CorrelationContextAccessor owner,
        string correlationId,
        CorrelationScope? parent) : IDisposable
    {
        private bool _disposed;

        public string CorrelationId { get; } = correlationId;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (ReferenceEquals(owner._current.Value, this))
            {
                owner._current.Value = parent;
            }

            _disposed = true;
        }
    }
}
