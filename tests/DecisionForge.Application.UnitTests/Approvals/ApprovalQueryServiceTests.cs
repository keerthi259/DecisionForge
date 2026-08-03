using DecisionForge.Application.Approvals;
using DecisionForge.Application.UnitTests.PurchaseRequests;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.Policies;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.UnitTests.Approvals;

public sealed class ApprovalQueryServiceTests
{
    [Fact]
    public async Task InboxUsesTrustedRoleScopeAndBoundedRequestedFilter()
    {
        StubApprovalAuthorization authorization = new()
        {
            Roles = [PolicyApproverRole.SecurityApprover, PolicyApproverRole.FinanceApprover],
        };
        RecordingApprovalQueries queries = new()
        {
            InboxResult = new ApprovalInboxResult(
                [InboxItem(PolicyApproverRole.FinanceApprover)],
                1,
                10,
                25),
        };
        StubCurrentUser currentUser = new(ApprovalServiceHarness.ActorId);
        ApprovalQueryService service = new(queries, authorization, currentUser);
        using CancellationTokenSource source = new();

        ApprovalInboxResult result = await service.ListInboxAsync(
            new ListApprovalInboxQuery(
                10,
                25,
                PolicyApproverRole.FinanceApprover,
                ApprovalInboxSortOrder.CreatedAtDescending),
            source.Token);

        Assert.Single(result.Items);
        Assert.Equal([PolicyApproverRole.FinanceApprover], queries.Roles);
        Assert.Equal(10, queries.Page!.Offset);
        Assert.Equal(25, queries.Page.PageSize);
        Assert.Equal(source.Token, queries.LastCancellationToken);
        Assert.Equal(ApprovalServiceHarness.ActorId, queries.UserId);
    }

    [Fact]
    public async Task UnauthorizedFilterAndInvalidPaginationAreRejectedBeforeQuery()
    {
        StubApprovalAuthorization authorization = new()
        {
            Roles = [PolicyApproverRole.SecurityApprover],
        };
        RecordingApprovalQueries queries = new();
        ApprovalQueryService service = new(
            queries,
            authorization,
            new StubCurrentUser(ApprovalServiceHarness.ActorId));

        DomainRuleException forbidden = await Assert.ThrowsAsync<DomainRuleException>(
            () => service.ListInboxAsync(
                new ListApprovalInboxQuery(
                    0,
                    20,
                    PolicyApproverRole.FinanceApprover,
                    ApprovalInboxSortOrder.CreatedAtDescending),
                CancellationToken.None));
        Assert.Equal(ApprovalApplicationErrorCodes.Forbidden, forbidden.Code);
        Assert.Equal(0, queries.ListCalls);

        await Assert.ThrowsAsync<DomainRuleException>(() => service.ListInboxAsync(
            new ListApprovalInboxQuery(-1, 20, null, ApprovalInboxSortOrder.CreatedAtDescending),
            CancellationToken.None));
        await Assert.ThrowsAsync<DomainRuleException>(() => service.ListInboxAsync(
            new ListApprovalInboxQuery(0, 101, null, ApprovalInboxSortOrder.CreatedAtDescending),
            CancellationToken.None));
        Assert.Equal(0, queries.ListCalls);
    }

    [Fact]
    public async Task UserWithoutApproverRolesReceivesEmptyInboxWithoutUnboundedQuery()
    {
        StubApprovalAuthorization authorization = new() { Roles = [] };
        RecordingApprovalQueries queries = new();
        ApprovalQueryService service = new(
            queries,
            authorization,
            new StubCurrentUser(ApprovalServiceHarness.ActorId));

        ApprovalInboxResult result = await service.ListInboxAsync(
            new ListApprovalInboxQuery(0, 100, null, ApprovalInboxSortOrder.CreatedAtAscending),
            CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, queries.ListCalls);
    }

    [Fact]
    public async Task DetailPassesTrustedRolesAndOverridePermissionAndRejectsScopeLeak()
    {
        Guid workflowId = Guid.Parse("12345678-1234-4234-8234-123456789012");
        StubApprovalAuthorization authorization = new()
        {
            Roles = [PolicyApproverRole.SecurityApprover],
        };
        RecordingApprovalQueries queries = new()
        {
            Detail = Detail(workflowId, PolicyApproverRole.SecurityApprover),
        };
        ApprovalQueryService service = new(
            queries,
            authorization,
            new StubCurrentUser(ApprovalServiceHarness.ActorId));

        ApprovalWorkflowDetail detail = await service.GetDetailAsync(
            new GetApprovalWorkflowDetailQuery(workflowId),
            CancellationToken.None);

        Assert.Equal(workflowId, detail.Id);
        Assert.Equal([PolicyApproverRole.SecurityApprover], queries.Roles);
        Assert.False(queries.CanOverride);

        queries.Detail = Detail(workflowId, PolicyApproverRole.FinanceApprover);
        DomainRuleException hidden = await Assert.ThrowsAsync<DomainRuleException>(
            () => service.GetDetailAsync(
                new GetApprovalWorkflowDetailQuery(workflowId),
                CancellationToken.None));
        Assert.Equal(ApprovalApplicationErrorCodes.NotFound, hidden.Code);

        authorization.CanOverride = true;
        ApprovalWorkflowDetail overrideDetail = await service.GetDetailAsync(
            new GetApprovalWorkflowDetailQuery(workflowId),
            CancellationToken.None);
        Assert.Equal(workflowId, overrideDetail.Id);
        Assert.True(queries.CanOverride);
    }

    [Fact]
    public async Task MissingDetailCancellationAndUnauthenticatedAccessAreControlled()
    {
        StubApprovalAuthorization authorization = new()
        {
            Roles = [PolicyApproverRole.FinanceApprover],
        };
        RecordingApprovalQueries queries = new();
        StubCurrentUser currentUser = new(ApprovalServiceHarness.ActorId);
        ApprovalQueryService service = new(queries, authorization, currentUser);
        DomainRuleException missing = await Assert.ThrowsAsync<DomainRuleException>(
            () => service.GetDetailAsync(
                new GetApprovalWorkflowDetailQuery(Guid.NewGuid()),
                CancellationToken.None));
        Assert.Equal(ApprovalApplicationErrorCodes.NotFound, missing.Code);

        currentUser.UserId = null;
        DomainRuleException anonymous = await Assert.ThrowsAsync<DomainRuleException>(
            () => service.ListInboxAsync(
                new ListApprovalInboxQuery(0, 20, null, ApprovalInboxSortOrder.CreatedAtDescending),
                CancellationToken.None));
        Assert.Equal(ApprovalApplicationErrorCodes.Unauthenticated, anonymous.Code);

        currentUser.UserId = ApprovalServiceHarness.ActorId;
        using CancellationTokenSource source = new();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ListInboxAsync(
                new ListApprovalInboxQuery(0, 20, null, ApprovalInboxSortOrder.CreatedAtDescending),
                source.Token));
    }

    private static ApprovalInboxItem InboxItem(PolicyApproverRole role)
    {
        return new ApprovalInboxItem(
            Guid.Parse("12345678-1234-4234-8234-123456789012"),
            Guid.Parse("22345678-1234-4234-8234-123456789012"),
            PurchaseRequestApplicationTestData.RequestId,
            RequestNumber.Parse("PR-2026-000001"),
            role,
            PurchaseRequestApplicationTestData.CurrentTime,
            PurchaseRequestApplicationTestData.Token(50));
    }

    private static ApprovalWorkflowDetail Detail(Guid workflowId, PolicyApproverRole role)
    {
        return new ApprovalWorkflowDetail(
            workflowId,
            PurchaseRequestApplicationTestData.RequestId,
            Guid.Parse("32345678-1234-4234-8234-123456789012"),
            RequestNumber.Parse("PR-2026-000001"),
            DecisionDisposition.ManualApprovalRequired,
            ApprovalWorkflowStatus.Active,
            [new ApprovalStageDetail(
                Guid.Parse("22345678-1234-4234-8234-123456789012"),
                1,
                role,
                ApprovalStageStatus.Pending,
                null,
                null,
                null,
                PurchaseRequestApplicationTestData.Token(50))],
            null,
            PurchaseRequestApplicationTestData.CurrentTime,
            null);
    }
}
