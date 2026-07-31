using DecisionForge.Domain.Common;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.ReferenceData;

internal static class ReferenceDataGuard
{
    public const int NameMaximumLength = 200;

    public static string Name(string value)
    {
        return StringValueValidation.Required(value, NameMaximumLength, nameof(value));
    }

    public static DateTimeOffset Mutation(
        ConcurrencyToken currentToken,
        ConcurrencyToken expectedToken,
        ConcurrencyToken nextToken,
        DateTimeOffset lastModifiedAt,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(expectedToken);
        ArgumentNullException.ThrowIfNull(nextToken);

        if (currentToken != expectedToken)
        {
            throw new DomainRuleException(
                DomainErrorCodes.ConcurrencyConflict,
                "The reference-data record was changed by another operation.");
        }

        if (currentToken == nextToken)
        {
            throw DomainGuard.Validation(
                nameof(nextToken),
                "The next concurrency token must differ from the current token.");
        }

        DateTimeOffset utcOccurredAt = DomainGuard.Utc(occurredAt, nameof(occurredAt));
        if (utcOccurredAt < lastModifiedAt)
        {
            throw DomainGuard.Validation(
                nameof(occurredAt),
                "Mutation time cannot precede the previous reference-data change.");
        }

        return utcOccurredAt;
    }
}
