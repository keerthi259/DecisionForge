using DecisionForge.Application.Platform;
using DecisionForge.Application.PurchaseRequests;
using DecisionForge.Application.PurchaseRequests.Ports;
using DecisionForge.Application.ReferenceData;
using DecisionForge.Application.ReferenceData.Ports;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.PurchaseRequests;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.UnitTests.PurchaseRequests;

internal sealed class RequestSequenceIdGenerator(params Guid[] values) : IIdGenerator
{
    private readonly Queue<Guid> _values = new(values);

    public int Calls { get; private set; }

    public Guid Create()
    {
        Calls++;
        return _values.Dequeue();
    }
}

internal sealed class RequestFixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow()
    {
        return utcNow;
    }
}

internal sealed class StubCurrentUser(Guid? userId) : ICurrentUserContext
{
    public Guid? UserId { get; set; } = userId;
}

internal sealed class StubRequestNumberGenerator(RequestNumber requestNumber)
    : IPurchaseRequestNumberGenerator
{
    public int Calls { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task<RequestNumber> ReserveNextAsync(
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        Calls++;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(requestNumber);
    }
}

internal sealed class RecordingPurchaseRequestRepository : IPurchaseRequestRepository
{
    public PurchaseRequest? Existing { get; set; }

    public PurchaseRequest? Added { get; private set; }

    public Guid? RequestedOwnerId { get; private set; }

    public int FindCalls { get; private set; }

    public int SaveCalls { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task<PurchaseRequest?> FindOwnedByIdAsync(
        Guid purchaseRequestId,
        Guid requesterId,
        CancellationToken cancellationToken)
    {
        FindCalls++;
        RequestedOwnerId = requesterId;
        LastCancellationToken = cancellationToken;
        PurchaseRequest? result = Existing is not null
            && Existing.Id == purchaseRequestId
            && Existing.RequesterId == requesterId
                ? Existing
                : null;
        return Task.FromResult(result);
    }

    public Task AddAsync(PurchaseRequest purchaseRequest, CancellationToken cancellationToken)
    {
        Added = purchaseRequest;
        LastCancellationToken = cancellationToken;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCalls++;
        LastCancellationToken = cancellationToken;
        return Task.CompletedTask;
    }
}

internal sealed class RecordingPurchaseRequestQueries : IPurchaseRequestQueries
{
    public PurchaseRequestListResult ListResult { get; set; } =
        new([], 0, 0, 20);

    public PurchaseRequestDetail? Detail { get; set; }

    public Guid? RequesterId { get; private set; }

    public PurchaseRequestPage? Page { get; private set; }

    public int ListCalls { get; private set; }

    public int DetailCalls { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task<PurchaseRequestListResult> ListForRequesterAsync(
        Guid requesterId,
        PurchaseRequestPage page,
        CancellationToken cancellationToken)
    {
        ListCalls++;
        RequesterId = requesterId;
        Page = page;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(ListResult);
    }

    public Task<PurchaseRequestDetail?> FindDetailForRequesterAsync(
        Guid purchaseRequestId,
        Guid requesterId,
        CancellationToken cancellationToken)
    {
        DetailCalls++;
        RequesterId = requesterId;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(Detail?.Id == purchaseRequestId ? Detail : null);
    }
}

internal sealed class StubDepartmentQueries : IDepartmentQueries
{
    public DepartmentLookup? Lookup { get; set; }

    public int FindCalls { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task<DepartmentLookup?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        FindCalls++;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(Lookup?.Id == id ? Lookup : null);
    }

    public Task<DepartmentLookup?> FindActiveByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        DepartmentLookup? result = Lookup is { IsActive: true } && Lookup.Id == id ? Lookup : null;
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<DepartmentLookup>> SearchActiveAsync(
        ReferenceDataPage page,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DepartmentLookup> result = Lookup is { IsActive: true } ? [Lookup] : [];
        return Task.FromResult(result);
    }
}

internal sealed class StubSupplierQueries : ISupplierQueries
{
    public SupplierLookup? Lookup { get; set; }

    public int FindCalls { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task<SupplierLookup?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        FindCalls++;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(Lookup?.Id == id ? Lookup : null);
    }

    public Task<SupplierLookup?> FindActiveByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        SupplierLookup? result = Lookup is { IsActive: true } && Lookup.Id == id ? Lookup : null;
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<SupplierLookup>> SearchActiveAsync(
        ReferenceDataPage page,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SupplierLookup> result = Lookup is { IsActive: true } ? [Lookup] : [];
        return Task.FromResult(result);
    }
}

internal static class PurchaseRequestApplicationTestData
{
    public static readonly Guid RequestId = Guid.Parse("11111111-1111-7111-8111-111111111111");
    public static readonly Guid RequesterId = Guid.Parse("22222222-2222-7222-8222-222222222222");
    public static readonly Guid DepartmentId = Guid.Parse("33333333-3333-7333-8333-333333333333");
    public static readonly Guid SupplierId = Guid.Parse("44444444-4444-7444-8444-444444444444");
    public static readonly DateTimeOffset InitialTime = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset CurrentTime = InitialTime.AddHours(1);
    public static readonly CurrencyCode Currency = CurrencyCode.Parse("INR");

    public static PurchaseRequest CreateRequest(bool withItem = false)
    {
        PurchaseRequest request = PurchaseRequest.Create(
            RequestId,
            RequestNumber.Parse("PR-2026-000001"),
            RequesterId,
            Currency,
            Metadata(),
            Token(0),
            InitialTime);
        if (withItem)
        {
            _ = request.AddItem(
                ItemId(1),
                "Developer laptop",
                2,
                Money.Create(1_250m, Currency),
                ProcurementCategory.Hardware,
                request.ConcurrencyToken,
                Token(1),
                InitialTime);
        }

        request.ClearDomainEvents();
        return request;
    }

    public static PurchaseRequestMetadata Metadata()
    {
        return PurchaseRequestMetadata.Create(
            DepartmentId,
            SupplierId,
            Urgency.Normal,
            DataSensitivity.Internal,
            new DateOnly(2026, 9, 1),
            BusinessJustification.Parse("Supports the delivery commitment."));
    }

    public static ConcurrencyToken Token(int sequence)
    {
        return ConcurrencyToken.Create(Guid.Parse($"55555555-5555-7555-8555-{sequence:000000000000}"));
    }

    public static Guid ItemId(int sequence)
    {
        return Guid.Parse($"66666666-6666-7666-8666-{sequence:000000000000}");
    }
}
