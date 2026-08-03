using DecisionForge.Domain.Enums;
using DecisionForge.Domain.PurchaseRequests;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.PurchaseRequests;

public sealed record CreatePurchaseRequestCommand(
    CurrencyCode Currency,
    PurchaseRequestMetadata Metadata);

public sealed record UpdatePurchaseRequestDraftCommand(
    Guid PurchaseRequestId,
    PurchaseRequestMetadata Metadata,
    ConcurrencyToken ExpectedToken);

public sealed record AddPurchaseRequestItemCommand(
    Guid PurchaseRequestId,
    string Description,
    int Quantity,
    Money UnitPrice,
    ProcurementCategory Category,
    ConcurrencyToken ExpectedToken);

public sealed record UpdatePurchaseRequestItemCommand(
    Guid PurchaseRequestId,
    Guid ItemId,
    string Description,
    int Quantity,
    Money UnitPrice,
    ProcurementCategory Category,
    ConcurrencyToken ExpectedToken);

public sealed record RemovePurchaseRequestItemCommand(
    Guid PurchaseRequestId,
    Guid ItemId,
    ConcurrencyToken ExpectedToken);

public sealed record WithdrawPurchaseRequestCommand(
    Guid PurchaseRequestId,
    ConcurrencyToken ExpectedToken);

public sealed record ClonePurchaseRequestCommand(
    Guid SourcePurchaseRequestId,
    ConcurrencyToken ExpectedSourceToken);
