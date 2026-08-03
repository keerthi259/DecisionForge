using DecisionForge.Domain.Audit;
using DecisionForge.Domain.Common;

namespace DecisionForge.Domain.Outbox;

public sealed class OutboxMessage : Entity
{
    public const int MaximumAttemptsLimit = 20;

    private OutboxMessage(
        Guid id,
        string messageType,
        AuditPayload payload,
        DateTimeOffset occurredAt,
        DateTimeOffset availableAt,
        int maximumAttempts)
        : base(id)
    {
        MessageType = messageType;
        Payload = payload;
        OccurredAt = occurredAt;
        AvailableAt = availableAt;
        MaximumAttempts = maximumAttempts;
        Status = OutboxStatus.Pending;
    }

    public string MessageType { get; }

    public AuditPayload Payload { get; }

    public DateTimeOffset OccurredAt { get; }

    public DateTimeOffset AvailableAt { get; private set; }

    public int Attempts { get; private set; }

    public int MaximumAttempts { get; }

    public OutboxStatus Status { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? LastErrorCode { get; private set; }

    public static OutboxMessage Create(
        Guid id,
        string messageType,
        AuditPayload payload,
        DateTimeOffset occurredAt,
        DateTimeOffset availableAt,
        int maximumAttempts)
    {
        ArgumentNullException.ThrowIfNull(payload);
        DomainGuard.Utc(occurredAt, nameof(occurredAt));
        DomainGuard.Utc(availableAt, nameof(availableAt));
        if (maximumAttempts is < 1 or > MaximumAttemptsLimit)
        {
            throw DomainGuard.Validation(
                nameof(maximumAttempts),
                $"Maximum attempts must be between 1 and {MaximumAttemptsLimit}.");
        }

        return new OutboxMessage(
            id,
            ControlledCode(messageType, 128, nameof(messageType)),
            payload,
            occurredAt,
            availableAt,
            maximumAttempts);
    }

    public void StartAttempt(DateTimeOffset attemptedAt)
    {
        DomainGuard.Utc(attemptedAt, nameof(attemptedAt));
        if (Status is OutboxStatus.Completed or OutboxStatus.Failed)
        {
            throw InvalidState("A terminal outbox message cannot be attempted.");
        }

        if (attemptedAt < AvailableAt)
        {
            throw InvalidState("The outbox message is not available yet.");
        }

        Attempts++;
        Status = OutboxStatus.Processing;
    }

    public bool Complete(DateTimeOffset completedAt)
    {
        DomainGuard.Utc(completedAt, nameof(completedAt));
        if (Status == OutboxStatus.Completed)
        {
            return false;
        }

        if (Status != OutboxStatus.Processing)
        {
            throw InvalidState("Only a processing outbox message can complete.");
        }

        Status = OutboxStatus.Completed;
        CompletedAt = completedAt;
        LastErrorCode = null;
        return true;
    }

    public bool RecordFailure(
        string errorCode,
        DateTimeOffset failedAt,
        DateTimeOffset nextAvailableAt)
    {
        DomainGuard.Utc(failedAt, nameof(failedAt));
        DomainGuard.Utc(nextAvailableAt, nameof(nextAvailableAt));
        if (Status != OutboxStatus.Processing)
        {
            throw InvalidState("Only a processing outbox message can fail.");
        }

        LastErrorCode = ControlledCode(errorCode, 64, nameof(errorCode));
        if (Attempts >= MaximumAttempts)
        {
            Status = OutboxStatus.Failed;
            AvailableAt = failedAt;
            return true;
        }

        if (nextAvailableAt <= failedAt)
        {
            throw DomainGuard.Validation(
                nameof(nextAvailableAt),
                "Retry availability must be later than the failure time.");
        }

        Status = OutboxStatus.Pending;
        AvailableAt = nextAvailableAt;
        return false;
    }

    private static string ControlledCode(string? value, int maximumLength, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is 0 || normalized.Length > maximumLength
            || normalized.Any(character =>
                !char.IsAsciiLetterOrDigit(character)
                && character is not '-' and not '_' and not '.'))
        {
            throw DomainGuard.Validation(parameterName, $"{parameterName} is invalid.");
        }

        return normalized;
    }

    private static DomainRuleException InvalidState(string message)
    {
        return new DomainRuleException("outbox-invalid-state", message);
    }
}
