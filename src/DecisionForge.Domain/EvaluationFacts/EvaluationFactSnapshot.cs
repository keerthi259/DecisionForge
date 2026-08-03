using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.PurchaseRequests;
using DecisionForge.Domain.ReferenceData;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.EvaluationFacts;

public sealed record RequestEvaluationFacts
{
    internal RequestEvaluationFacts(
        decimal totalAmount,
        CurrencyCode currency,
        ProcurementCategory category,
        Urgency urgency,
        DataSensitivity dataSensitivity,
        int itemCount,
        int expectedDeliveryDays,
        bool hasBusinessJustification)
    {
        TotalAmount = totalAmount;
        Currency = currency;
        Category = category;
        Urgency = urgency;
        DataSensitivity = dataSensitivity;
        ItemCount = itemCount;
        ExpectedDeliveryDays = expectedDeliveryDays;
        HasBusinessJustification = hasBusinessJustification;
    }

    public decimal TotalAmount { get; }

    public CurrencyCode Currency { get; }

    public ProcurementCategory Category { get; }

    public Urgency Urgency { get; }

    public DataSensitivity DataSensitivity { get; }

    public int ItemCount { get; }

    public int ExpectedDeliveryDays { get; }

    public bool HasBusinessJustification { get; }
}

public sealed record DepartmentEvaluationFacts
{
    internal DepartmentEvaluationFacts(DepartmentCode code, decimal autoApprovalLimit)
    {
        Code = code;
        AutoApprovalLimit = autoApprovalLimit;
    }

    public DepartmentCode Code { get; }

    public decimal AutoApprovalLimit { get; }
}

public sealed record DepartmentEvaluationSource
{
    private DepartmentEvaluationSource(
        Guid id,
        DepartmentCode code,
        Money autoApprovalLimit,
        bool isActive)
    {
        Id = id;
        Code = code;
        AutoApprovalLimit = autoApprovalLimit;
        IsActive = isActive;
    }

    public Guid Id { get; }

    public DepartmentCode Code { get; }

    public Money AutoApprovalLimit { get; }

    public bool IsActive { get; }

    public static DepartmentEvaluationSource Create(
        Guid id,
        DepartmentCode code,
        Money autoApprovalLimit,
        bool isActive)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(autoApprovalLimit);
        return new DepartmentEvaluationSource(id, code, autoApprovalLimit, isActive);
    }
}

public sealed record SupplierEvaluationFacts
{
    internal SupplierEvaluationFacts(
        bool isApproved,
        SupplierOnboardingStatus onboardingStatus,
        SupplierRiskRating riskRating,
        bool isActive)
    {
        IsApproved = isApproved;
        OnboardingStatus = onboardingStatus;
        RiskRating = riskRating;
        IsActive = isActive;
    }

    public bool IsApproved { get; }

    public SupplierOnboardingStatus OnboardingStatus { get; }

    public SupplierRiskRating RiskRating { get; }

    public bool IsActive { get; }
}

public sealed record SupplierEvaluationSource
{
    private SupplierEvaluationSource(
        Guid id,
        SupplierApprovalStatus approvalStatus,
        SupplierOnboardingStatus onboardingStatus,
        SupplierRiskRating riskRating,
        bool isActive)
    {
        Id = id;
        ApprovalStatus = approvalStatus;
        OnboardingStatus = onboardingStatus;
        RiskRating = riskRating;
        IsActive = isActive;
    }

    public Guid Id { get; }

    public SupplierApprovalStatus ApprovalStatus { get; }

    public SupplierOnboardingStatus OnboardingStatus { get; }

    public SupplierRiskRating RiskRating { get; }

    public bool IsActive { get; }

    public static SupplierEvaluationSource Create(
        Guid id,
        SupplierApprovalStatus approvalStatus,
        SupplierOnboardingStatus onboardingStatus,
        SupplierRiskRating riskRating,
        bool isActive)
    {
        DomainGuard.NotEmpty(id, nameof(id));
        return new SupplierEvaluationSource(
            id,
            approvalStatus,
            onboardingStatus,
            riskRating,
            isActive);
    }
}

public sealed record DerivedEvaluationFacts
{
    internal DerivedEvaluationFacts(
        bool containsTechnologyPurchase,
        bool requiresUrgencyException)
    {
        ContainsTechnologyPurchase = containsTechnologyPurchase;
        RequiresUrgencyException = requiresUrgencyException;
    }

    public bool ContainsTechnologyPurchase { get; }

    public bool RequiresUrgencyException { get; }
}

public sealed record EvaluationFactSnapshot
{
    private EvaluationFactSnapshot(
        RequestEvaluationFacts request,
        DepartmentEvaluationFacts department,
        SupplierEvaluationFacts supplier,
        DerivedEvaluationFacts derived)
    {
        Request = request;
        Department = department;
        Supplier = supplier;
        Derived = derived;
    }

    public RequestEvaluationFacts Request { get; }

    public DepartmentEvaluationFacts Department { get; }

    public SupplierEvaluationFacts Supplier { get; }

    public DerivedEvaluationFacts Derived { get; }

    public static EvaluationFactSnapshot Create(
        PurchaseRequest purchaseRequest,
        Department department,
        Supplier supplier,
        DateOnly evaluationDate)
    {
        ArgumentNullException.ThrowIfNull(purchaseRequest);
        ArgumentNullException.ThrowIfNull(department);
        ArgumentNullException.ThrowIfNull(supplier);
        return Create(
            purchaseRequest,
            DepartmentEvaluationSource.Create(
                department.Id,
                department.Code,
                department.AutoApprovalLimit,
                department.IsActive),
            SupplierEvaluationSource.Create(
                supplier.Id,
                supplier.ApprovalStatus,
                supplier.OnboardingStatus,
                supplier.RiskRating,
                supplier.IsActive),
            evaluationDate);
    }

    public static EvaluationFactSnapshot Create(
        PurchaseRequest purchaseRequest,
        DepartmentEvaluationSource department,
        SupplierEvaluationSource supplier,
        DateOnly evaluationDate)
    {
        ArgumentNullException.ThrowIfNull(purchaseRequest);
        ArgumentNullException.ThrowIfNull(department);
        ArgumentNullException.ThrowIfNull(supplier);
        EnsureReferencesMatch(purchaseRequest, department.Id, supplier.Id);
        EnsureActive(department.IsActive, supplier.IsActive);

        if (purchaseRequest.Items.Count == 0)
        {
            throw new DomainRuleException(
                DomainErrorCodes.InvalidState,
                "Evaluation facts require at least one purchase-request item.");
        }

        if (purchaseRequest.Currency != department.AutoApprovalLimit.Currency)
        {
            throw new DomainRuleException(
                DomainErrorCodes.CurrencyMismatch,
                "Department auto-approval currency must match the request currency.");
        }

        int expectedDeliveryDays =
            purchaseRequest.Metadata.ExpectedDeliveryDate.DayNumber - evaluationDate.DayNumber;
        if (expectedDeliveryDays < 0)
        {
            throw DomainGuard.Validation(
                nameof(evaluationDate),
                "Expected delivery date cannot precede the evaluation date.");
        }

        ProcurementCategory category = ResolveCategory(purchaseRequest.Items);
        RequestEvaluationFacts requestFacts = new(
            purchaseRequest.Total.Amount,
            purchaseRequest.Currency,
            category,
            purchaseRequest.Metadata.Urgency,
            purchaseRequest.Metadata.DataSensitivity,
            CalculateItemCount(purchaseRequest.Items),
            expectedDeliveryDays,
            purchaseRequest.Metadata.BusinessJustification is not null);
        DepartmentEvaluationFacts departmentFacts = new(
            department.Code,
            department.AutoApprovalLimit.Amount);
        SupplierEvaluationFacts supplierFacts = new(
            supplier.ApprovalStatus == SupplierApprovalStatus.Approved,
            supplier.OnboardingStatus,
            supplier.RiskRating,
            supplier.IsActive);
        DerivedEvaluationFacts derivedFacts = new(
            purchaseRequest.Items.Any(item => IsTechnology(item.Category)),
            purchaseRequest.Metadata.Urgency is Urgency.Urgent or Urgency.Emergency);

        return new EvaluationFactSnapshot(
            requestFacts,
            departmentFacts,
            supplierFacts,
            derivedFacts);
    }

    private static void EnsureReferencesMatch(
        PurchaseRequest purchaseRequest,
        Guid departmentId,
        Guid supplierId)
    {
        if (purchaseRequest.Metadata.DepartmentId != departmentId
            || purchaseRequest.Metadata.SupplierId != supplierId)
        {
            throw new DomainRuleException(
                DomainErrorCodes.ReferenceMismatch,
                "Reference data does not match the purchase request.");
        }
    }

    private static void EnsureActive(bool departmentIsActive, bool supplierIsActive)
    {
        if (!departmentIsActive)
        {
            throw new DomainRuleException(
                DomainErrorCodes.InactiveReference,
                "The selected department is inactive.");
        }

        if (!supplierIsActive)
        {
            throw new DomainRuleException(
                DomainErrorCodes.InactiveReference,
                "The selected supplier is inactive.");
        }
    }

    private static ProcurementCategory ResolveCategory(
        IReadOnlyCollection<PurchaseRequestItem> items)
    {
        ProcurementCategory[] categories = items
            .Select(item => item.Category)
            .Distinct()
            .Take(2)
            .ToArray();
        return categories.Length == 1 ? categories[0] : ProcurementCategory.Other;
    }

    private static int CalculateItemCount(IEnumerable<PurchaseRequestItem> items)
    {
        try
        {
            return items.Sum(item => item.Quantity);
        }
        catch (OverflowException)
        {
            throw new DomainRuleException(
                DomainErrorCodes.AmountOverflow,
                "Purchase-request item count exceeds the supported integer range.");
        }
    }

    private static bool IsTechnology(ProcurementCategory category)
    {
        return category is ProcurementCategory.Software
            or ProcurementCategory.Hardware
            or ProcurementCategory.CloudService;
    }
}
