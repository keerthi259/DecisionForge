using DecisionForge.Application.Reliability.Outbox;

namespace DecisionForge.Application.UnitTests.Reliability;

public sealed class OutboxDispatcherTests
{
    private static readonly DateTimeOffset _now = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SuccessfulMessageCompletesAndDuplicateCompletionIsNotApplied()
    {
        RecordingStore store = new(Item(attempt: 1, maximumAttempts: 5));
        RecordingHandler handler = new();
        OutboxDispatcher dispatcher = Create(store, handler);

        OutboxDispatchResult result = await dispatcher.DispatchOnceAsync(CancellationToken.None);

        Assert.Equal(new OutboxDispatchResult(1, 1, 0, 0), result);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(1, store.CompleteCalls);
        Assert.True(await store.CompleteAsync(
            store.Item.MessageId,
            store.Item.LeaseToken,
            _now,
            CancellationToken.None) is false);
    }

    [Fact]
    public async Task DeliveryFailureUsesBoundedBackoffThenBecomesTerminal()
    {
        RecordingStore retryStore = new(Item(attempt: 2, maximumAttempts: 3));
        RecordingHandler failing = new() { Exception = new InvalidOperationException("sensitive") };
        OutboxDispatchResult retried = await Create(retryStore, failing).DispatchOnceAsync(
            CancellationToken.None);

        Assert.Equal(1, retried.Retried);
        Assert.Equal("outbox-delivery-failed", retryStore.ErrorCode);
        Assert.Equal(_now.AddSeconds(10), retryStore.NextAvailableAt);

        RecordingStore terminalStore = new(Item(attempt: 3, maximumAttempts: 3));
        OutboxDispatchResult terminal = await Create(terminalStore, failing).DispatchOnceAsync(
            CancellationToken.None);
        Assert.Equal(1, terminal.TerminalFailures);
    }

    [Fact]
    public async Task MissingHandlerHasStableErrorAndCleanupUsesRetentionCutoff()
    {
        RecordingStore store = new(Item(attempt: 1, maximumAttempts: 2));
        OutboxDispatcher dispatcher = Create(store);

        OutboxDispatchResult result = await dispatcher.DispatchOnceAsync(CancellationToken.None);
        int deleted = await dispatcher.CleanupCompletedAsync(CancellationToken.None);

        Assert.Equal(1, result.Retried);
        Assert.Equal("outbox-handler-not-found", store.ErrorCode);
        Assert.Equal(1, deleted);
        Assert.Equal(_now.AddDays(-7), store.CleanupCutoff);
    }

    [Fact]
    public async Task CancellationIsPropagatedWithoutRecordingFailure()
    {
        RecordingStore store = new(Item(attempt: 1, maximumAttempts: 2));
        RecordingHandler handler = new() { Exception = new OperationCanceledException() };
        using CancellationTokenSource source = new();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Create(store, handler).DispatchOnceAsync(source.Token));
        Assert.Null(store.ErrorCode);
    }

    private static OutboxDispatcher Create(
        RecordingStore store,
        params IOutboxMessageHandler[] handlers)
    {
        return new OutboxDispatcher(
            store,
            handlers,
            new FixedTimeProvider(_now),
            new OutboxDispatcherOptions());
    }

    private static OutboxWorkItem Item(int attempt, int maximumAttempts)
    {
        return new OutboxWorkItem(
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            "test.message.v1",
            "{}",
            _now,
            attempt,
            maximumAttempts,
            Guid.Parse("22222222-2222-4222-8222-222222222222"));
    }

    private sealed class RecordingHandler : IOutboxMessageHandler
    {
        public string MessageType => "test.message.v1";

        public Exception? Exception { get; init; }

        public int Calls { get; private set; }

        public Task HandleAsync(OutboxWorkItem message, CancellationToken cancellationToken)
        {
            Calls++;
            return Exception is null ? Task.CompletedTask : Task.FromException(Exception);
        }
    }

    private sealed class RecordingStore(OutboxWorkItem item) : IOutboxStore
    {
        private bool _completed;

        public OutboxWorkItem Item { get; } = item;

        public int CompleteCalls { get; private set; }

        public string? ErrorCode { get; private set; }

        public DateTimeOffset? NextAvailableAt { get; private set; }

        public DateTimeOffset? CleanupCutoff { get; private set; }

        public Task<IReadOnlyList<OutboxWorkItem>> ClaimAsync(
            DateTimeOffset now,
            int batchSize,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<OutboxWorkItem>>([Item]);
        }

        public Task<bool> CompleteAsync(
            Guid messageId,
            Guid leaseToken,
            DateTimeOffset completedAt,
            CancellationToken cancellationToken)
        {
            CompleteCalls++;
            bool changed = !_completed;
            _completed = true;
            return Task.FromResult(changed);
        }

        public Task<OutboxFailureResult> RecordFailureAsync(
            Guid messageId,
            Guid leaseToken,
            string errorCode,
            DateTimeOffset failedAt,
            DateTimeOffset nextAvailableAt,
            CancellationToken cancellationToken)
        {
            ErrorCode = errorCode;
            NextAvailableAt = nextAvailableAt;
            return Task.FromResult(new OutboxFailureResult(
                Item.Attempt >= Item.MaximumAttempts,
                Item.Attempt));
        }

        public Task<int> DeleteCompletedAsync(
            DateTimeOffset completedBefore,
            int batchSize,
            CancellationToken cancellationToken)
        {
            CleanupCutoff = completedBefore;
            return Task.FromResult(1);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return value;
        }
    }
}
