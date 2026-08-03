using System.Security.Claims;
using DecisionForge.Api.Authorization;
using DecisionForge.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace DecisionForge.Api.IntegrationTests.Identity;

[Collection(IdentityApiTestGroup.Name)]
public sealed class ResourceAuthorizationTests(IdentityApiFixture fixture)
{
    private static readonly Guid _ownerId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid _otherId = Guid.Parse("22222222-2222-4222-8222-222222222222");

    [Fact]
    public async Task PurchaseRequestOwnerApproverAuditorAndDenialMatrixIsEnforced()
    {
        IAuthorizationService authorization = fixture.Factory.Services
            .GetRequiredService<IAuthorizationService>();
        PurchaseRequestAuthorizationResource draft = new(
            _ownerId,
            IsDraft: true,
            [DecisionForgeIdentityRoles.FinanceApprover]);
        PurchaseRequestAuthorizationResource submitted = draft with { IsDraft = false };
        PurchaseRequestAuthorizationResource malformed = new(
            _ownerId,
            IsDraft: false,
            AssignedApproverRoles: null!);
        ClaimsPrincipal owner = Principal(_ownerId, DecisionForgeIdentityRoles.Requester);
        ClaimsPrincipal otherRequester = Principal(_otherId, DecisionForgeIdentityRoles.Requester);
        ClaimsPrincipal assignedApprover = Principal(_otherId, DecisionForgeIdentityRoles.FinanceApprover);
        ClaimsPrincipal wrongApprover = Principal(_otherId, DecisionForgeIdentityRoles.SecurityApprover);
        ClaimsPrincipal auditor = Principal(_otherId, DecisionForgeIdentityRoles.Auditor);
        ClaimsPrincipal administrator = Principal(_otherId, DecisionForgeIdentityRoles.Administrator);

        Assert.True((await AuthorizeAsync(
            authorization,
            owner,
            draft,
            AuthorizationPolicyNames.CanReadPurchaseRequest)).Succeeded);
        Assert.True((await AuthorizeAsync(
            authorization,
            owner,
            draft,
            AuthorizationPolicyNames.CanEditPurchaseRequest)).Succeeded);
        Assert.True((await AuthorizeAsync(
            authorization,
            owner,
            draft,
            AuthorizationPolicyNames.CanSubmitPurchaseRequest)).Succeeded);
        Assert.False((await AuthorizeAsync(
            authorization,
            otherRequester,
            draft,
            AuthorizationPolicyNames.CanReadPurchaseRequest)).Succeeded);
        Assert.True((await AuthorizeAsync(
            authorization,
            assignedApprover,
            submitted,
            AuthorizationPolicyNames.CanReadPurchaseRequest)).Succeeded);
        Assert.False((await AuthorizeAsync(
            authorization,
            assignedApprover,
            draft,
            AuthorizationPolicyNames.CanEditPurchaseRequest)).Succeeded);
        Assert.False((await AuthorizeAsync(
            authorization,
            wrongApprover,
            submitted,
            AuthorizationPolicyNames.CanReadPurchaseRequest)).Succeeded);
        Assert.True((await AuthorizeAsync(
            authorization,
            auditor,
            submitted,
            AuthorizationPolicyNames.CanReadPurchaseRequest)).Succeeded);
        Assert.False((await AuthorizeAsync(
            authorization,
            auditor,
            submitted,
            AuthorizationPolicyNames.CanEditPurchaseRequest)).Succeeded);
        Assert.False((await AuthorizeAsync(
            authorization,
            administrator,
            submitted,
            AuthorizationPolicyNames.CanReadPurchaseRequest)).Succeeded);
        Assert.False((await AuthorizeAsync(
            authorization,
            owner,
            submitted,
            AuthorizationPolicyNames.CanEditPurchaseRequest)).Succeeded);
        Assert.False((await AuthorizeAsync(
            authorization,
            owner,
            submitted,
            AuthorizationPolicyNames.CanSubmitPurchaseRequest)).Succeeded);
        Assert.False((await AuthorizeAsync(
            authorization,
            wrongApprover,
            malformed,
            AuthorizationPolicyNames.CanReadPurchaseRequest)).Succeeded);
    }

    [Fact]
    public async Task ApprovalStageRequiresMatchingRoleAndPendingState()
    {
        IAuthorizationService authorization = fixture.Factory.Services
            .GetRequiredService<IAuthorizationService>();
        ClaimsPrincipal finance = Principal(_ownerId, DecisionForgeIdentityRoles.FinanceApprover);
        ClaimsPrincipal security = Principal(_otherId, DecisionForgeIdentityRoles.SecurityApprover);
        ClaimsPrincipal administrator = Principal(_otherId, DecisionForgeIdentityRoles.Administrator);
        ApprovalStageAuthorizationResource pending = new(
            DecisionForgeIdentityRoles.FinanceApprover,
            IsPending: true);
        ApprovalStageAuthorizationResource completed = pending with { IsPending = false };

        Assert.True((await AuthorizeAsync(
            authorization,
            finance,
            pending,
            AuthorizationPolicyNames.CanActOnApprovalStage)).Succeeded);
        Assert.False((await AuthorizeAsync(
            authorization,
            security,
            pending,
            AuthorizationPolicyNames.CanActOnApprovalStage)).Succeeded);
        Assert.False((await AuthorizeAsync(
            authorization,
            finance,
            completed,
            AuthorizationPolicyNames.CanActOnApprovalStage)).Succeeded);
        Assert.False((await AuthorizeAsync(
            authorization,
            administrator,
            pending,
            AuthorizationPolicyNames.CanActOnApprovalStage)).Succeeded);
    }

    [Fact]
    public async Task PolicyAuditAdminAndOverridePoliciesRemainSeparated()
    {
        IAuthorizationService authorization = fixture.Factory.Services
            .GetRequiredService<IAuthorizationService>();
        ClaimsPrincipal requester = Principal(_ownerId, DecisionForgeIdentityRoles.Requester);
        ClaimsPrincipal author = Principal(_ownerId, DecisionForgeIdentityRoles.PolicyAuthor);
        ClaimsPrincipal publisher = Principal(_ownerId, DecisionForgeIdentityRoles.PolicyPublisher);
        ClaimsPrincipal auditor = Principal(_ownerId, DecisionForgeIdentityRoles.Auditor);
        ClaimsPrincipal administrator = Principal(_ownerId, DecisionForgeIdentityRoles.Administrator);
        ClaimsPrincipal seniorWithoutPermission = Principal(
            _ownerId,
            DecisionForgeIdentityRoles.SeniorApprover);
        ClaimsPrincipal explicitOverride = Principal(
            _ownerId,
            DecisionForgeIdentityRoles.SeniorApprover,
            DecisionForgeIdentityPermissions.OverrideDecision);

        Assert.True((await authorization.AuthorizeAsync(
            requester,
            resource: null,
            AuthorizationPolicyNames.CanCreateRequest)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(
            administrator,
            resource: null,
            AuthorizationPolicyNames.CanCreateRequest)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(
            author,
            resource: null,
            AuthorizationPolicyNames.CanAuthorPolicy)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(
            author,
            resource: null,
            AuthorizationPolicyNames.CanPublishPolicy)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(
            publisher,
            resource: null,
            AuthorizationPolicyNames.CanPublishPolicy)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(
            publisher,
            resource: null,
            AuthorizationPolicyNames.CanAuthorPolicy)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(
            auditor,
            resource: null,
            AuthorizationPolicyNames.CanReadAudit)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(
            auditor,
            resource: null,
            AuthorizationPolicyNames.CanManageReferenceData)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(
            administrator,
            resource: null,
            AuthorizationPolicyNames.CanManageReferenceData)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(
            administrator,
            resource: null,
            AuthorizationPolicyNames.CanReadAudit)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(
            seniorWithoutPermission,
            resource: null,
            AuthorizationPolicyNames.CanOverrideDecision)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(
            administrator,
            resource: null,
            AuthorizationPolicyNames.CanOverrideDecision)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(
            explicitOverride,
            resource: null,
            AuthorizationPolicyNames.CanOverrideDecision)).Succeeded);
    }

    private static Task<AuthorizationResult> AuthorizeAsync(
        IAuthorizationService authorization,
        ClaimsPrincipal principal,
        object resource,
        string policyName)
    {
        return authorization.AuthorizeAsync(principal, resource, policyName);
    }

    private static ClaimsPrincipal Principal(
        Guid userId,
        string role,
        string? permission = null)
    {
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, userId.ToString("D")),
            new(ClaimTypes.Name, $"{role}@decisionforge.local"),
            new(ClaimTypes.Role, role),
        ];
        if (permission is not null)
        {
            claims.Add(new Claim(DecisionForgeIdentityPermissions.ClaimType, permission));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            authenticationType: "Phase13Test",
            ClaimTypes.Name,
            ClaimTypes.Role));
    }
}
