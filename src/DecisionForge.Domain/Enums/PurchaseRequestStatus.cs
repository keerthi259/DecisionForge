namespace DecisionForge.Domain.Enums;

public enum PurchaseRequestStatus
{
    Draft,
    Submitted,
    Evaluating,
    AutoApproved,
    PendingApproval,
    Approved,
    Rejected,
    Withdrawn,
    EvaluationFailed,
}
