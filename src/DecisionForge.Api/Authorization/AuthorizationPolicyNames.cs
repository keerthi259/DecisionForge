namespace DecisionForge.Api.Authorization;

public static class AuthorizationPolicyNames
{
    public const string CanCreateRequest = "CanCreateRequest";
    public const string CanReadPurchaseRequest = "CanReadPurchaseRequest";
    public const string CanEditPurchaseRequest = "CanEditPurchaseRequest";
    public const string CanSubmitPurchaseRequest = "CanSubmitPurchaseRequest";
    public const string CanActOnApprovalStage = "CanActOnApprovalStage";
    public const string CanAuthorPolicy = "CanAuthorPolicy";
    public const string CanPublishPolicy = "CanPublishPolicy";
    public const string CanReadAudit = "CanReadAudit";
    public const string CanManageReferenceData = "CanManageReferenceData";
    public const string CanOverrideDecision = "CanOverrideDecision";
}
