using DecisionForge.Domain.Common;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.Approvals;

internal static class ApprovalWorkflowGuard
{
    public static DateTimeOffset ActionTime(
        DateTimeOffset lastModifiedAt,
        DateTimeOffset occurredAt)
    {
        DateTimeOffset utcOccurredAt = DomainGuard.Utc(occurredAt, nameof(occurredAt));
        if (utcOccurredAt < lastModifiedAt)
        {
            throw DomainGuard.Validation(
                nameof(occurredAt),
                "Approval action time cannot precede the previous workflow change.");
        }

        return utcOccurredAt;
    }

    public static string? OptionalNote(string? note)
    {
        return string.IsNullOrWhiteSpace(note)
            ? null
            : ValidLength(note.Trim(), nameof(note));
    }

    public static string RequiredNote(
        string? note,
        string errorCode,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            throw new DomainRuleException(
                errorCode,
                "A non-empty reason is required.",
                parameterName);
        }

        return ValidLength(note.Trim(), parameterName);
    }

    public static void ValidateStageIdentity(
        int count,
        IReadOnlyCollection<Guid> stageIds,
        IReadOnlyCollection<ConcurrencyToken> stageTokens)
    {
        if (stageIds.Count != count
            || stageTokens.Count != count
            || stageIds.Any(id => id == Guid.Empty)
            || stageIds.Distinct().Count() != count
            || stageTokens.Any(token => token is null)
            || stageTokens.Distinct().Count() != count)
        {
            throw DomainGuard.Validation(
                nameof(stageIds),
                "Stage identities and tokens must be non-empty, unique and match the role plan.");
        }
    }

    private static string ValidLength(string note, string parameterName)
    {
        if (note.Length > ApprovalWorkflow.MaximumNoteLength)
        {
            throw DomainGuard.Validation(
                parameterName,
                $"Approval text must not exceed {ApprovalWorkflow.MaximumNoteLength} characters.");
        }

        return note;
    }
}
