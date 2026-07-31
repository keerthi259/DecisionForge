using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.PurchaseRequests;

public sealed record PurchaseRequestMetadata
{
    private PurchaseRequestMetadata(
        Guid departmentId,
        Guid supplierId,
        Urgency urgency,
        DataSensitivity dataSensitivity,
        DateOnly expectedDeliveryDate,
        BusinessJustification? businessJustification)
    {
        DepartmentId = departmentId;
        SupplierId = supplierId;
        Urgency = urgency;
        DataSensitivity = dataSensitivity;
        ExpectedDeliveryDate = expectedDeliveryDate;
        BusinessJustification = businessJustification;
    }

    public Guid DepartmentId { get; }

    public Guid SupplierId { get; }

    public Urgency Urgency { get; }

    public DataSensitivity DataSensitivity { get; }

    public DateOnly ExpectedDeliveryDate { get; }

    public BusinessJustification? BusinessJustification { get; }

    public static PurchaseRequestMetadata Create(
        Guid departmentId,
        Guid supplierId,
        Urgency urgency,
        DataSensitivity dataSensitivity,
        DateOnly expectedDeliveryDate,
        BusinessJustification? businessJustification)
    {
        DomainGuard.NotEmpty(departmentId, nameof(departmentId));
        DomainGuard.NotEmpty(supplierId, nameof(supplierId));

        if (!Enum.IsDefined(urgency))
        {
            throw DomainGuard.Validation(nameof(urgency), "Urgency is not supported.");
        }

        if (!Enum.IsDefined(dataSensitivity))
        {
            throw DomainGuard.Validation(
                nameof(dataSensitivity),
                "Data sensitivity is not supported.");
        }

        if (expectedDeliveryDate == default)
        {
            throw DomainGuard.Validation(
                nameof(expectedDeliveryDate),
                "Expected delivery date must be specified.");
        }

        return new PurchaseRequestMetadata(
            departmentId,
            supplierId,
            urgency,
            dataSensitivity,
            expectedDeliveryDate,
            businessJustification);
    }
}
