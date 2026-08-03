using DecisionForge.Domain.Common;

namespace DecisionForge.Application.PurchaseRequests.Idempotency;

public sealed record SubmissionFingerprint
{
    public const int Length = 64;

    private SubmissionFingerprint(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static SubmissionFingerprint Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length != Length
            || value.Any(character => character is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f')))
        {
            throw new DomainRuleException(
                DomainErrorCodes.Validation,
                "Submission fingerprint must be a lowercase SHA-256 hexadecimal value.",
                nameof(value));
        }

        return new SubmissionFingerprint(value);
    }

    public override string ToString()
    {
        return Value;
    }
}
