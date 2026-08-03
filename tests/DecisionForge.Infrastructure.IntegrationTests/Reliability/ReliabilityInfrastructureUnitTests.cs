using System.Net;
using DecisionForge.Application.Reliability.Notifications;
using DecisionForge.Application.Reliability.Outbox;
using DecisionForge.Infrastructure.Reliability;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DecisionForge.Infrastructure.IntegrationTests.Reliability;

public sealed class ReliabilityInfrastructureUnitTests
{
    private static readonly DateTimeOffset _now = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ReliabilityOptionsValidateEveryBoundedSetting()
    {
        Assert.True(new ReliabilityOptions().IsValid());
        Assert.False(new ReliabilityOptions { MailpitBaseAddress = "file:///secret" }.IsValid());
        Assert.False(new ReliabilityOptions { SenderEmail = "invalid" }.IsValid());
        Assert.False(new ReliabilityOptions { BatchSize = 0 }.IsValid());
        Assert.False(new ReliabilityOptions { PollIntervalSeconds = 0 }.IsValid());
        Assert.False(new ReliabilityOptions { CompletedRetentionDays = 0 }.IsValid());
    }

    [Fact]
    public async Task MailpitFailureBecomesControlledDeliveryError()
    {
        using HttpClient client = new(new StubHttpHandler(HttpStatusCode.ServiceUnavailable))
        {
            BaseAddress = new Uri("http://localhost:8025", UriKind.Absolute),
        };
        MailpitNotificationSender sender = new(
            client,
            Options.Create(new ReliabilityOptions()));

        OutboxDeliveryException exception = await Assert.ThrowsAsync<OutboxDeliveryException>(
            () => sender.SendAsync(
                new NotificationDelivery(
                    Guid.NewGuid(), "user@example.com", "Subject", "Body"),
                CancellationToken.None));
        Assert.Equal("notification-transport-failed", exception.ErrorCode);
    }

    [Fact]
    public async Task DisabledWorkerExitsWithoutTouchingStore()
    {
        WorkerStore store = new();
        OutboxWorker worker = Worker(store, enabled: false);

        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(0, store.ClaimCalls);
    }

    [Fact]
    public async Task EnabledWorkerRecordsTerminalFailureAndCleansCompletedRows()
    {
        WorkerStore store = new
        (
            new OutboxWorkItem(
                Guid.NewGuid(), "worker.test.v1", "{}", _now, 1, 1, Guid.NewGuid())
        );
        OutboxWorker worker = Worker(store, enabled: true, new FailingHandler());

        await worker.StartAsync(CancellationToken.None);
        await store.CleanupReached.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal("worker-failed", store.ErrorCode);
        Assert.True(store.CleanupCalls >= 1);
    }

    [Fact]
    public async Task WorkerRecoversFromCycleFailureUntilStopped()
    {
        WorkerStore store = new() { ClaimException = new InvalidOperationException("database down") };
        OutboxWorker worker = Worker(store, enabled: true);

        await worker.StartAsync(CancellationToken.None);
        await store.ClaimReached.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);

        Assert.True(store.ClaimCalls >= 1);
    }

    private static OutboxWorker Worker(
        WorkerStore store,
        bool enabled,
        params IOutboxMessageHandler[] handlers)
    {
        OutboxDispatcher dispatcher = new(
            store,
            handlers,
            new FixedTimeProvider(),
            new OutboxDispatcherOptions { PollInterval = TimeSpan.FromHours(1) });
        return new OutboxWorker(
            dispatcher,
            Options.Create(new ReliabilityOptions
            {
                DispatcherEnabled = enabled,
                PollIntervalSeconds = 300,
            }),
            TimeProvider.System,
            NullLogger<OutboxWorker>.Instance);
    }

    private sealed class FailingHandler : IOutboxMessageHandler
    {
        public string MessageType => "worker.test.v1";

        public Task HandleAsync(OutboxWorkItem message, CancellationToken cancellationToken)
        {
            throw new OutboxDeliveryException("worker-failed", "Controlled failure.");
        }
    }

    private sealed class WorkerStore : IOutboxStore
    {
        private readonly OutboxWorkItem? _item;

        public WorkerStore(OutboxWorkItem? item = null)
        {
            _item = item;
        }

        public TaskCompletionSource ClaimReached { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource CleanupReached { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Exception? ClaimException { get; init; }

        public int ClaimCalls { get; private set; }

        public int CleanupCalls { get; private set; }

        public string? ErrorCode { get; private set; }

        public Task<IReadOnlyList<OutboxWorkItem>> ClaimAsync(
            DateTimeOffset now,
            int batchSize,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken)
        {
            ClaimCalls++;
            ClaimReached.TrySetResult();
            return ClaimException is null
                ? Task.FromResult<IReadOnlyList<OutboxWorkItem>>(
                    _item is null ? [] : [_item])
                : Task.FromException<IReadOnlyList<OutboxWorkItem>>(ClaimException);
        }

        public Task<bool> CompleteAsync(
            Guid messageId,
            Guid leaseToken,
            DateTimeOffset completedAt,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
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
            return Task.FromResult(new OutboxFailureResult(true, 1));
        }

        public Task<int> DeleteCompletedAsync(
            DateTimeOffset completedBefore,
            int batchSize,
            CancellationToken cancellationToken)
        {
            CleanupCalls++;
            CleanupReached.TrySetResult();
            return Task.FromResult(0);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }
    }

    private sealed class StubHttpHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }
}
