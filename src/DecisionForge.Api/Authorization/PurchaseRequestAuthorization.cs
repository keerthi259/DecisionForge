using Microsoft.AspNetCore.Authorization;

namespace DecisionForge.Api.Authorization;

public sealed record PurchaseRequestAuthorizationResource(
    Guid RequesterId,
    bool IsDraft,
    IReadOnlyCollection<string> AssignedApproverRoles);

public enum PurchaseRequestAuthorizationOperation
{
    Read,
    Edit,
    Submit,
}

public sealed record PurchaseRequestAuthorizationRequirement(
    PurchaseRequestAuthorizationOperation Operation) : IAuthorizationRequirement;
