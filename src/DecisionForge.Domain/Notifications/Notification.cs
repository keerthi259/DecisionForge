using DecisionForge.Domain.Common;

namespace DecisionForge.Domain.Notifications;

public sealed class Notification : Entity
{
    private Notification(
        Guid id,
        Guid userId,
        Guid sourceOutboxMessageId,
        string emailAddress,
        string subject,
        string body,
        string relativeLink,
        DateTimeOffset createdAt)
        : base(id)
    {
        UserId = userId;
        SourceOutboxMessageId = sourceOutboxMessageId;
        EmailAddress = emailAddress;
        Subject = subject;
        Body = body;
        RelativeLink = relativeLink;
        CreatedAt = createdAt;
    }

    public Guid UserId { get; }

    public Guid SourceOutboxMessageId { get; }

    public string EmailAddress { get; }

    public string Subject { get; }

    public string Body { get; }

    public string RelativeLink { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? ReadAt { get; private set; }

    public bool IsRead => ReadAt is not null;

    public static Notification Create(
        Guid id,
        Guid userId,
        Guid sourceOutboxMessageId,
        string emailAddress,
        string subject,
        string body,
        string relativeLink,
        DateTimeOffset createdAt)
    {
        DomainGuard.NotEmpty(userId, nameof(userId));
        DomainGuard.NotEmpty(sourceOutboxMessageId, nameof(sourceOutboxMessageId));
        DomainGuard.Utc(createdAt, nameof(createdAt));
        string normalizedEmail = Required(emailAddress, 254, nameof(emailAddress));
        if (normalizedEmail.Count(character => character == '@') != 1
            || normalizedEmail.Any(char.IsWhiteSpace))
        {
            throw DomainGuard.Validation(nameof(emailAddress), "Notification email is invalid.");
        }

        return new Notification(
            id,
            userId,
            sourceOutboxMessageId,
            normalizedEmail,
            Required(subject, 160, nameof(subject)),
            Required(body, 1_000, nameof(body)),
            RelativeLinkValue(relativeLink),
            createdAt);
    }

    public bool MarkRead(DateTimeOffset readAt)
    {
        DomainGuard.Utc(readAt, nameof(readAt));
        if (ReadAt is not null)
        {
            return false;
        }

        ReadAt = readAt;
        return true;
    }

    private static string Required(string? value, int maximumLength, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is 0 || normalized.Length > maximumLength)
        {
            throw DomainGuard.Validation(parameterName, $"{parameterName} is invalid.");
        }

        return normalized;
    }

    private static string RelativeLinkValue(string? value)
    {
        string normalized = Required(value, 512, nameof(value));
        if (!normalized.StartsWith('/')
            || normalized.StartsWith("//", StringComparison.Ordinal)
            || Uri.TryCreate(normalized, UriKind.Absolute, out _))
        {
            throw DomainGuard.Validation(nameof(value), "Notification link must be application-relative.");
        }

        return normalized;
    }
}
