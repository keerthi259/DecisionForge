using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.Decisions;

public sealed record DecisionReason
{
    internal DecisionReason(ReasonCode code, string message)
    {
        Code = code;
        Message = message;
    }

    public ReasonCode Code { get; }

    public string Message { get; }
}
