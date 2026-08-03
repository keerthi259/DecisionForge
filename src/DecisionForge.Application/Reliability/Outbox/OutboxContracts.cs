namespace DecisionForge.Application.Reliability.Outbox;

public sealed record OutboxWorkItem(
    Guid MessageId,
    string MessageType,
    string CanonicalPayload,
    DateTimeOffset OccurredAt,
    int Attempt,
    int MaximumAttempts,
    Guid LeaseToken);

public sealed record OutboxFailureResult(bool IsTerminal, int Attempt);

public sealed record OutboxDispatchResult(
    int Claimed,
    int Completed,
    int Retried,
    int TerminalFailures);

public interface IOutboxStore
{
    Task<IReadOnlyList<OutboxWorkItem>> ClaimAsync(
        DateTimeOffset now,
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task<bool> CompleteAsync(
        Guid messageId,
        Guid leaseToken,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken);

    Task<OutboxFailureResult> RecordFailureAsync(
        Guid messageId,
        Guid leaseToken,
        string errorCode,
        DateTimeOffset failedAt,
        DateTimeOffset nextAvailableAt,
        CancellationToken cancellationToken);

    Task<int> DeleteCompletedAsync(
        DateTimeOffset completedBefore,
        int batchSize,
        CancellationToken cancellationToken);
}

public interface IOutboxMessageHandler
{
    string MessageType { get; }

    Task HandleAsync(OutboxWorkItem message, CancellationToken cancellationToken);
}

public sealed class OutboxDeliveryException : Exception
{
    public OutboxDeliveryException(string errorCode, string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        if (errorCode.Length > 64 || errorCode.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new ArgumentException("Outbox error code is invalid.", nameof(errorCode));
        }

        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}
