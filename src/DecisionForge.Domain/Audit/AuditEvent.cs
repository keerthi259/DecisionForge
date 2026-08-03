using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.Audit;

public sealed class AuditEvent : Entity
{
    private AuditEvent(
        Guid eventId,
        long sequence,
        string aggregateType,
        Guid aggregateId,
        string eventType,
        string actor,
        DateTimeOffset occurredAt,
        CorrelationId correlationId,
        AuditPayload payload,
        AuditHash previousHash,
        AuditHash hash)
        : base(eventId)
    {
        Sequence = sequence;
        AggregateType = aggregateType;
        AggregateId = aggregateId;
        EventType = eventType;
        Actor = actor;
        OccurredAt = occurredAt;
        CorrelationId = correlationId;
        Payload = payload;
        PreviousHash = previousHash;
        Hash = hash;
    }

    public long Sequence { get; }

    public string AggregateType { get; }

    public Guid AggregateId { get; }

    public string EventType { get; }

    public string Actor { get; }

    public DateTimeOffset OccurredAt { get; }

    public CorrelationId CorrelationId { get; }

    public AuditPayload Payload { get; }

    public AuditHash PreviousHash { get; }

    public AuditHash Hash { get; }

    public static AuditEvent Create(
        Guid eventId,
        long sequence,
        string aggregateType,
        Guid aggregateId,
        string eventType,
        string actor,
        DateTimeOffset occurredAt,
        CorrelationId correlationId,
        AuditPayload payload,
        AuditHash previousHash)
    {
        Validated values = Validate(
            sequence,
            aggregateType,
            aggregateId,
            eventType,
            actor,
            occurredAt,
            correlationId,
            payload,
            previousHash);
        DateTimeOffset normalizedOccurredAt = NormalizeForPostgres(occurredAt);
        AuditHash hash = CalculateHash(
            sequence,
            eventId,
            values.AggregateType,
            aggregateId,
            values.EventType,
            values.Actor,
            normalizedOccurredAt,
            correlationId,
            payload,
            previousHash);
        return new AuditEvent(
            eventId,
            sequence,
            values.AggregateType,
            aggregateId,
            values.EventType,
            values.Actor,
            normalizedOccurredAt,
            correlationId,
            payload,
            previousHash,
            hash);
    }

    public static AuditEvent Restore(
        Guid eventId,
        long sequence,
        string aggregateType,
        Guid aggregateId,
        string eventType,
        string actor,
        DateTimeOffset occurredAt,
        CorrelationId correlationId,
        AuditPayload payload,
        AuditHash previousHash,
        AuditHash hash)
    {
        ArgumentNullException.ThrowIfNull(hash);
        Validated values = Validate(
            sequence,
            aggregateType,
            aggregateId,
            eventType,
            actor,
            occurredAt,
            correlationId,
            payload,
            previousHash);
        DateTimeOffset normalizedOccurredAt = NormalizeForPostgres(occurredAt);
        return new AuditEvent(
            eventId,
            sequence,
            values.AggregateType,
            aggregateId,
            values.EventType,
            values.Actor,
            normalizedOccurredAt,
            correlationId,
            payload,
            previousHash,
            hash);
    }

    public AuditHash RecalculateHash()
    {
        return CalculateHash(
            Sequence,
            Id,
            AggregateType,
            AggregateId,
            EventType,
            Actor,
            OccurredAt,
            CorrelationId,
            Payload,
            PreviousHash);
    }

    private static AuditHash CalculateHash(
        long sequence,
        Guid eventId,
        string aggregateType,
        Guid aggregateId,
        string eventType,
        string actor,
        DateTimeOffset occurredAt,
        CorrelationId correlationId,
        AuditPayload payload,
        AuditHash previousHash)
    {
        string input = string.Concat(
            sequence.ToString(CultureInfo.InvariantCulture),
            eventId.ToString("D"),
            aggregateType,
            aggregateId.ToString("D"),
            eventType,
            actor,
            occurredAt.ToString("O", CultureInfo.InvariantCulture),
            correlationId.Value,
            payload.CanonicalJson,
            previousHash.Value);
        return AuditHash.Parse(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input))));
    }

    private static Validated Validate(
        long sequence,
        string aggregateType,
        Guid aggregateId,
        string eventType,
        string actor,
        DateTimeOffset occurredAt,
        CorrelationId correlationId,
        AuditPayload payload,
        AuditHash previousHash)
    {
        if (sequence <= 0)
        {
            throw DomainGuard.Validation(nameof(sequence), "Audit sequence must be positive.");
        }

        DomainGuard.NotEmpty(aggregateId, nameof(aggregateId));
        DomainGuard.Utc(occurredAt, nameof(occurredAt));
        ArgumentNullException.ThrowIfNull(correlationId);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(previousHash);
        return new Validated(
            ControlledText(aggregateType, 64, nameof(aggregateType)),
            ControlledText(eventType, 128, nameof(eventType)),
            ControlledText(actor, 128, nameof(actor)));
    }

    private static string ControlledText(string? value, int maximumLength, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is 0 || normalized.Length > maximumLength
            || normalized.Any(character =>
                !char.IsAsciiLetterOrDigit(character)
                && character is not '-' and not '_' and not '.' and not ':'))
        {
            throw DomainGuard.Validation(parameterName, $"{parameterName} is invalid.");
        }

        return normalized;
    }

    private static DateTimeOffset NormalizeForPostgres(DateTimeOffset value)
    {
        const long ticksPerMicrosecond = 10;
        long ticks = value.Ticks - (value.Ticks % ticksPerMicrosecond);
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }

    private sealed record Validated(string AggregateType, string EventType, string Actor);
}
