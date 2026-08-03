using DecisionForge.Application.Platform;
using DecisionForge.Application.PurchaseRequests.Ports;
using DecisionForge.Domain.Common;

namespace DecisionForge.Application.PurchaseRequests;

public sealed class PurchaseRequestQueryService
{
    private readonly IPurchaseRequestQueries _queries;
    private readonly ICurrentUserContext _currentUser;

    public PurchaseRequestQueryService(
        IPurchaseRequestQueries queries,
        ICurrentUserContext currentUser)
    {
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(currentUser);
        _queries = queries;
        _currentUser = currentUser;
    }

    public async Task<PurchaseRequestListResult> ListOwnAsync(
        ListPurchaseRequestsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        Guid requesterId = TrustedRequester.RequiredUserId(_currentUser);
        PurchaseRequestPage page = PurchaseRequestPage.Create(
            query.Offset,
            query.PageSize,
            query.Status,
            query.SortOrder);
        return await _queries.ListForRequesterAsync(requesterId, page, cancellationToken);
    }

    public async Task<PurchaseRequestDetail> GetOwnDetailAsync(
        GetPurchaseRequestDetailQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        Guid requesterId = TrustedRequester.RequiredUserId(_currentUser);
        PurchaseRequestDetail? detail = await _queries.FindDetailForRequesterAsync(
            query.PurchaseRequestId,
            requesterId,
            cancellationToken);
        return detail
            ?? throw new DomainRuleException(
                PurchaseRequestApplicationErrorCodes.NotFound,
                $"Purchase request '{query.PurchaseRequestId}' was not found.",
                nameof(query.PurchaseRequestId));
    }
}
