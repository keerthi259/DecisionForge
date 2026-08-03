namespace DecisionForge.Application.Reliability.Outbox;

public sealed class OutboxDispatcher
{
    private readonly IOutboxStore _store;
    private readonly Dictionary<string, IOutboxMessageHandler> _handlers;
    private readonly TimeProvider _timeProvider;
    private readonly OutboxDispatcherOptions _options;

    public OutboxDispatcher(
        IOutboxStore store,
        IEnumerable<IOutboxMessageHandler> handlers,
        TimeProvider timeProvider,
        OutboxDispatcherOptions options)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(handlers);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _store = store;
        _timeProvider = timeProvider;
        _options = options;
        _handlers = handlers.ToDictionary(handler => handler.MessageType, StringComparer.Ordinal);
    }

    public async Task<OutboxDispatchResult> DispatchOnceAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DateTimeOffset now = _timeProvider.GetUtcNow();
        IReadOnlyList<OutboxWorkItem> messages = await _store.ClaimAsync(
            now,
            _options.BatchSize,
            _options.LeaseDuration,
            cancellationToken);
        int completed = 0;
        int retried = 0;
        int terminal = 0;
        foreach (OutboxWorkItem message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DispatchOutcome outcome = await DispatchMessageAsync(message, cancellationToken);
            completed += outcome == DispatchOutcome.Completed ? 1 : 0;
            retried += outcome == DispatchOutcome.Retry ? 1 : 0;
            terminal += outcome == DispatchOutcome.Terminal ? 1 : 0;
        }

        return new OutboxDispatchResult(messages.Count, completed, retried, terminal);
    }

    public Task<int> CleanupCompletedAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset cutoff = _timeProvider.GetUtcNow() - _options.CompletedRetention;
        return _store.DeleteCompletedAsync(cutoff, _options.BatchSize, cancellationToken);
    }

    private async Task<DispatchOutcome> DispatchMessageAsync(
        OutboxWorkItem message,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_handlers.TryGetValue(message.MessageType, out IOutboxMessageHandler? handler))
            {
                throw new OutboxDeliveryException(
                    "outbox-handler-not-found",
                    "No handler is registered for the message type.");
            }

            await handler.HandleAsync(message, cancellationToken);
            bool completed = await _store.CompleteAsync(
                message.MessageId,
                message.LeaseToken,
                _timeProvider.GetUtcNow(),
                cancellationToken);
            return completed ? DispatchOutcome.Completed : DispatchOutcome.LeaseLost;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            string errorCode = exception is OutboxDeliveryException delivery
                ? delivery.ErrorCode
                : "outbox-delivery-failed";
            DateTimeOffset failedAt = _timeProvider.GetUtcNow();
            TimeSpan delay = RetryDelay(message.Attempt);
            OutboxFailureResult result = await _store.RecordFailureAsync(
                message.MessageId,
                message.LeaseToken,
                errorCode,
                failedAt,
                failedAt + delay,
                cancellationToken);
            return result.IsTerminal ? DispatchOutcome.Terminal : DispatchOutcome.Retry;
        }
    }

    private TimeSpan RetryDelay(int attempt)
    {
        int exponent = Math.Clamp(attempt - 1, 0, 30);
        double ticks = _options.InitialRetryDelay.Ticks * Math.Pow(2, exponent);
        return TimeSpan.FromTicks((long)Math.Min(ticks, _options.MaximumRetryDelay.Ticks));
    }

    private enum DispatchOutcome
    {
        Completed,
        Retry,
        Terminal,
        LeaseLost,
    }
}
