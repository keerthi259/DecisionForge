using Microsoft.AspNetCore.Authorization;

namespace DecisionForge.Api.Authorization;

public sealed record ApprovalStageAuthorizationResource(
    string RequiredRole,
    bool IsPending);

public sealed class ActOnApprovalStageRequirement : IAuthorizationRequirement;
