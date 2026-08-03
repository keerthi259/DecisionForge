using DecisionForge.Domain.Common;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.Policies.Lifecycle;

internal static class PolicyLifecycleGuard
{
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
                "The policy was changed by another operation.");
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
                "Policy mutation time cannot precede the previous change.");
        }

        return utcOccurredAt;
    }

    public static string Name(string? value)
    {
        return StringValueValidation.Required(
            value,
            PolicyContractLimits.MaximumPolicyNameLength,
            nameof(value));
    }

    public static void Draft(PolicyVersion version)
    {
        if (version.Status != Enums.PolicyStatus.Draft)
        {
            throw new DomainRuleException(
                PolicyLifecycleErrorCodes.ImmutableVersion,
                "Published and retired policy versions are immutable.");
        }
    }
}
