namespace DecisionForge.Domain.Approvals;

public static class ApprovalErrorCodes
{
    public const string ManualDecisionRequired = "approval.manual-decision-required";
    public const string RolesRequired = "approval.roles-required";
    public const string StageNotFound = "approval.stage-not-found";
    public const string NotActionable = "approval.not-actionable";
    public const string RoleMismatch = "approval.role-mismatch";
    public const string RejectionReasonRequired = "approval.rejection-reason-required";
    public const string OverrideReasonRequired = "approval.override-reason-required";
}
