using DecisionForge.Domain.Decisions;

namespace DecisionForge.Application.Decisions;

public sealed class DecisionSubmissionResult
{
    public DecisionSubmissionResult(Decision decision, bool isReplay)
    {
        ArgumentNullException.ThrowIfNull(decision);
        Decision = decision;
        IsReplay = isReplay;
    }

    public Decision Decision { get; }

    public bool IsReplay { get; }
}
