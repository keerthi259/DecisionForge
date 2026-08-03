using DecisionForge.Application.PurchaseRequests;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.PurchaseRequests;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.UnitTests.PurchaseRequests;

public sealed class PurchaseRequestQueryServiceTests
{
    [Fact]
    public async Task ListUsesTrustedRequesterAndBoundedAllowListedPage()
    {
        RecordingPurchaseRequestQueries queries = new();
        PurchaseRequestQueryService service = CreateService(queries);
        using CancellationTokenSource source = new();

        PurchaseRequestListResult result = await service.ListOwnAsync(
            new ListPurchaseRequestsQuery(
                20,
                25,
                PurchaseRequestStatus.Draft,
                PurchaseRequestSortOrder.CreatedAtAscending),
            source.Token);

        Assert.Same(queries.ListResult, result);
        Assert.Equal(PurchaseRequestApplicationTestData.RequesterId, queries.RequesterId);
        Assert.Equal(20, queries.Page!.Offset);
        Assert.Equal(25, queries.Page.PageSize);
        Assert.Equal(PurchaseRequestStatus.Draft, queries.Page.Status);
        Assert.Equal(PurchaseRequestSortOrder.CreatedAtAscending, queries.Page.SortOrder);
        Assert.Equal(source.Token, queries.LastCancellationToken);
    }

    [Theory]
    [InlineData(-1, 20, PurchaseRequestSortOrder.CreatedAtDescending)]
    [InlineData(0, 0, PurchaseRequestSortOrder.CreatedAtDescending)]
    [InlineData(0, 101, PurchaseRequestSortOrder.CreatedAtDescending)]
    [InlineData(0, 20, (PurchaseRequestSortOrder)999)]
    public async Task ListRejectsInvalidPaginationAndSortBeforePortCall(
        int offset,
        int pageSize,
        PurchaseRequestSortOrder sortOrder)
    {
        RecordingPurchaseRequestQueries queries = new();
        PurchaseRequestQueryService service = CreateService(queries);

        DomainRuleException exception = await Assert.ThrowsAsync<DomainRuleException>(
            () => service.ListOwnAsync(
                new ListPurchaseRequestsQuery(offset, pageSize, null, sortOrder),
                CancellationToken.None));

        Assert.Equal(DomainErrorCodes.Validation, exception.Code);
        Assert.Equal(0, queries.ListCalls);
    }

    [Fact]
    public async Task ListAcceptsMaximumPageSize()
    {
        RecordingPurchaseRequestQueries queries = new();
        PurchaseRequestQueryService service = CreateService(queries);

        await service.ListOwnAsync(
            new ListPurchaseRequestsQuery(
                0,
                PurchaseRequestPage.MaximumPageSize,
                null,
                PurchaseRequestSortOrder.CreatedAtDescending),
            CancellationToken.None);

        Assert.Equal(PurchaseRequestPage.MaximumPageSize, queries.Page!.PageSize);
    }

    [Fact]
    public async Task ListRejectsUnknownStatusBeforePortCall()
    {
        RecordingPurchaseRequestQueries queries = new();
        PurchaseRequestQueryService service = CreateService(queries);

        DomainRuleException exception = await Assert.ThrowsAsync<DomainRuleException>(
            () => service.ListOwnAsync(
                new ListPurchaseRequestsQuery(
                    0,
                    20,
                    (PurchaseRequestStatus)999,
                    PurchaseRequestSortOrder.CreatedAtDescending),
                CancellationToken.None));

        Assert.Equal(DomainErrorCodes.Validation, exception.Code);
        Assert.Equal(0, queries.ListCalls);
    }

    [Fact]
    public async Task DetailUsesTrustedRequesterAndReturnsExplicitProjection()
    {
        PurchaseRequestDetail detail = Detail();
        RecordingPurchaseRequestQueries queries = new() { Detail = detail };
        PurchaseRequestQueryService service = CreateService(queries);

        PurchaseRequestDetail result = await service.GetOwnDetailAsync(
            new GetPurchaseRequestDetailQuery(detail.Id),
            CancellationToken.None);

        Assert.Same(detail, result);
        Assert.Equal(PurchaseRequestApplicationTestData.RequesterId, queries.RequesterId);
        Assert.Equal(1, queries.DetailCalls);
    }

    [Fact]
    public async Task MissingOrUnauthorizedDetailUsesSameNonDisclosingError()
    {
        RecordingPurchaseRequestQueries queries = new();
        PurchaseRequestQueryService service = CreateService(queries);

        DomainRuleException exception = await Assert.ThrowsAsync<DomainRuleException>(
            () => service.GetOwnDetailAsync(
                new GetPurchaseRequestDetailQuery(PurchaseRequestApplicationTestData.RequestId),
                CancellationToken.None));

        Assert.Equal(PurchaseRequestApplicationErrorCodes.NotFound, exception.Code);
        Assert.DoesNotContain("unauthorized", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnauthenticatedAndPreCancelledQueriesDoNotCallPort()
    {
        RecordingPurchaseRequestQueries unauthenticatedQueries = new();
        PurchaseRequestQueryService unauthenticated = new(
            unauthenticatedQueries,
            new StubCurrentUser(null));
        DomainRuleException exception = await Assert.ThrowsAsync<DomainRuleException>(
            () => unauthenticated.ListOwnAsync(
                new ListPurchaseRequestsQuery(
                    0,
                    20,
                    null,
                    PurchaseRequestSortOrder.CreatedAtDescending),
                CancellationToken.None));

        RecordingPurchaseRequestQueries cancelledQueries = new();
        PurchaseRequestQueryService cancelled = CreateService(cancelledQueries);
        using CancellationTokenSource source = new();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cancelled.GetOwnDetailAsync(
                new GetPurchaseRequestDetailQuery(PurchaseRequestApplicationTestData.RequestId),
                source.Token));

        Assert.Equal(PurchaseRequestApplicationErrorCodes.Unauthenticated, exception.Code);
        Assert.Equal(0, unauthenticatedQueries.ListCalls);
        Assert.Equal(0, cancelledQueries.DetailCalls);
    }

    [Fact]
    public void ResultsDefensivelyCopyCollectionsAndValidateBounds()
    {
        List<PurchaseRequestSummary> summaries = [Summary()];
        PurchaseRequestListResult result = new(summaries, 1, 0, 20);
        summaries.Clear();
        ICollection<PurchaseRequestSummary> exposed =
            Assert.IsAssignableFrom<ICollection<PurchaseRequestSummary>>(result.Items);

        Assert.Single(result.Items);
        Assert.True(exposed.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => exposed.Add(Summary()));
        Assert.Throws<ArgumentException>(
            () => new PurchaseRequestListResult([Summary(), Summary()], 1, 0, 20));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PurchaseRequestListResult([], 0, 0, 101));
    }

    private static PurchaseRequestQueryService CreateService(
        RecordingPurchaseRequestQueries queries)
    {
        return new PurchaseRequestQueryService(
            queries,
            new StubCurrentUser(PurchaseRequestApplicationTestData.RequesterId));
    }

    private static PurchaseRequestDetail Detail()
    {
        PurchaseRequestMetadata metadata = PurchaseRequestApplicationTestData.Metadata();
        Money unitPrice = Money.Create(10m, PurchaseRequestApplicationTestData.Currency);
        return new PurchaseRequestDetail(
            PurchaseRequestApplicationTestData.RequestId,
            RequestNumber.Parse("PR-2026-000001"),
            PurchaseRequestStatus.Draft,
            PurchaseRequestApplicationTestData.Currency,
            metadata,
            Money.Create(20m, PurchaseRequestApplicationTestData.Currency),
            [
                new PurchaseRequestItemDetail(
                    PurchaseRequestApplicationTestData.ItemId(1),
                    "Cable",
                    2,
                    unitPrice,
                    Money.Create(20m, PurchaseRequestApplicationTestData.Currency),
                    ProcurementCategory.Hardware),
            ],
            PurchaseRequestApplicationTestData.InitialTime,
            PurchaseRequestApplicationTestData.InitialTime,
            null,
            PurchaseRequestApplicationTestData.Token(0));
    }

    private static PurchaseRequestSummary Summary()
    {
        return new PurchaseRequestSummary(
            PurchaseRequestApplicationTestData.RequestId,
            RequestNumber.Parse("PR-2026-000001"),
            PurchaseRequestStatus.Draft,
            Money.Zero(PurchaseRequestApplicationTestData.Currency),
            PurchaseRequestApplicationTestData.InitialTime,
            null,
            PurchaseRequestApplicationTestData.Token(0));
    }
}
