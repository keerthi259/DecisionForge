using DecisionForge.Application.Platform;
using DecisionForge.Application.PurchaseRequests.Ports;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.PurchaseRequests;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.PurchaseRequests;

public sealed class PurchaseRequestLifecycleService
{
    private readonly IPurchaseRequestRepository _repository;
    private readonly IPurchaseRequestNumberGenerator _numberGenerator;
    private readonly ICurrentUserContext _currentUser;
    private readonly IIdGenerator _idGenerator;
    private readonly TimeProvider _timeProvider;

    public PurchaseRequestLifecycleService(
        IPurchaseRequestRepository repository,
        IPurchaseRequestNumberGenerator numberGenerator,
        ICurrentUserContext currentUser,
        IIdGenerator idGenerator,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(numberGenerator);
        ArgumentNullException.ThrowIfNull(currentUser);
        ArgumentNullException.ThrowIfNull(idGenerator);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _repository = repository;
        _numberGenerator = numberGenerator;
        _currentUser = currentUser;
        _idGenerator = idGenerator;
        _timeProvider = timeProvider;
    }

    public async Task<PurchaseRequest> CreateAsync(
        CreatePurchaseRequestCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        Guid requesterId = TrustedRequester.RequiredUserId(_currentUser);
        DateTimeOffset occurredAt = _timeProvider.GetUtcNow();
        RequestNumber requestNumber = await _numberGenerator.ReserveNextAsync(
            occurredAt,
            cancellationToken);
        PurchaseRequest request = PurchaseRequest.Create(
            _idGenerator.Create(),
            requestNumber,
            requesterId,
            command.Currency,
            command.Metadata,
            NextToken(),
            occurredAt);
        await _repository.AddAsync(request, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<PurchaseRequest> UpdateDraftAsync(
        UpdatePurchaseRequestDraftCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        PurchaseRequest request = await FindOwnedAsync(command.PurchaseRequestId, cancellationToken);
        ConcurrencyToken previousToken = request.ConcurrencyToken;
        request.UpdateMetadata(
            command.Metadata,
            command.ExpectedToken,
            NextToken(),
            _timeProvider.GetUtcNow());
        await SaveIfChangedAsync(request, previousToken, cancellationToken);
        return request;
    }

    public async Task<PurchaseRequestItem> AddItemAsync(
        AddPurchaseRequestItemCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        PurchaseRequest request = await FindOwnedAsync(command.PurchaseRequestId, cancellationToken);
        PurchaseRequestItem item = request.AddItem(
            _idGenerator.Create(),
            command.Description,
            command.Quantity,
            command.UnitPrice,
            command.Category,
            command.ExpectedToken,
            NextToken(),
            _timeProvider.GetUtcNow());
        await _repository.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task<PurchaseRequestItem> UpdateItemAsync(
        UpdatePurchaseRequestItemCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        PurchaseRequest request = await FindOwnedAsync(command.PurchaseRequestId, cancellationToken);
        ConcurrencyToken previousToken = request.ConcurrencyToken;
        request.UpdateItem(
            command.ItemId,
            command.Description,
            command.Quantity,
            command.UnitPrice,
            command.Category,
            command.ExpectedToken,
            NextToken(),
            _timeProvider.GetUtcNow());
        await SaveIfChangedAsync(request, previousToken, cancellationToken);
        return request.Items.Single(item => item.Id == command.ItemId);
    }

    public async Task<PurchaseRequest> RemoveItemAsync(
        RemovePurchaseRequestItemCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        PurchaseRequest request = await FindOwnedAsync(command.PurchaseRequestId, cancellationToken);
        request.RemoveItem(
            command.ItemId,
            command.ExpectedToken,
            NextToken(),
            _timeProvider.GetUtcNow());
        await _repository.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<PurchaseRequest> WithdrawAsync(
        WithdrawPurchaseRequestCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        PurchaseRequest request = await FindOwnedAsync(command.PurchaseRequestId, cancellationToken);
        request.Withdraw(
            command.ExpectedToken,
            NextToken(),
            _timeProvider.GetUtcNow());
        await _repository.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<PurchaseRequest> CloneAsync(
        ClonePurchaseRequestCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        Guid requesterId = TrustedRequester.RequiredUserId(_currentUser);
        PurchaseRequest source = await FindOwnedAsync(
            command.SourcePurchaseRequestId,
            requesterId,
            cancellationToken);
        EnsureExpectedSourceToken(source, command.ExpectedSourceToken);
        DateTimeOffset occurredAt = _timeProvider.GetUtcNow();
        RequestNumber requestNumber = await _numberGenerator.ReserveNextAsync(
            occurredAt,
            cancellationToken);
        Guid cloneId = _idGenerator.Create();
        ConcurrencyToken cloneToken = NextToken();
        Guid[] itemIds = source.Items.Select(_ => _idGenerator.Create()).ToArray();
        PurchaseRequest clone = source.Clone(
            cloneId,
            requestNumber,
            requesterId,
            itemIds,
            cloneToken,
            occurredAt);
        await _repository.AddAsync(clone, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return clone;
    }

    private Task<PurchaseRequest> FindOwnedAsync(Guid id, CancellationToken cancellationToken)
    {
        return FindOwnedAsync(id, TrustedRequester.RequiredUserId(_currentUser), cancellationToken);
    }

    private async Task<PurchaseRequest> FindOwnedAsync(
        Guid id,
        Guid requesterId,
        CancellationToken cancellationToken)
    {
        PurchaseRequest? request = await _repository.FindOwnedByIdAsync(
            id,
            requesterId,
            cancellationToken);
        return request
            ?? throw new DomainRuleException(
                PurchaseRequestApplicationErrorCodes.NotFound,
                $"Purchase request '{id}' was not found.",
                nameof(id));
    }

    private static void EnsureExpectedSourceToken(
        PurchaseRequest source,
        ConcurrencyToken expectedToken)
    {
        ArgumentNullException.ThrowIfNull(expectedToken);
        if (source.ConcurrencyToken != expectedToken)
        {
            throw new DomainRuleException(
                DomainErrorCodes.ConcurrencyConflict,
                "The source purchase request was changed by another operation.");
        }
    }

    private async Task SaveIfChangedAsync(
        PurchaseRequest request,
        ConcurrencyToken previousToken,
        CancellationToken cancellationToken)
    {
        if (request.ConcurrencyToken != previousToken)
        {
            await _repository.SaveChangesAsync(cancellationToken);
        }
    }

    private ConcurrencyToken NextToken()
    {
        return ConcurrencyToken.Create(_idGenerator.Create());
    }
}
