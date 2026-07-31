using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.EvaluationFacts;
using DecisionForge.Domain.PurchaseRequests;
using DecisionForge.Domain.ReferenceData;
using DecisionForge.Domain.UnitTests.Builders;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.UnitTests.EvaluationFacts;

public sealed class EvaluationFactSnapshotTests
{
    private static readonly DateOnly _evaluationDate = new(2026, 8, 1);

    [Fact]
    public void FlagshipScenarioProducesExactApprovedFacts()
    {
        PurchaseRequest request = new PurchaseRequestBuilder()
            .WithMetadata(Metadata(
                Urgency.Urgent,
                DataSensitivity.Internal,
                new DateOnly(2026, 8, 31),
                BusinessJustification.Parse("Urgent customer project.")))
            .WithItem(quantity: 30, unitPrice: 80_000m, category: ProcurementCategory.Hardware)
            .Build();
        Department department = new DepartmentBuilder().Build();
        Supplier supplier = new SupplierBuilder()
            .WithStates(
                SupplierApprovalStatus.Pending,
                SupplierOnboardingStatus.InProgress,
                SupplierRiskRating.Medium)
            .Build();

        EvaluationFactSnapshot snapshot = EvaluationFactSnapshot.Create(
            request,
            department,
            supplier,
            _evaluationDate);

        Assert.Equal(2_400_000m, snapshot.Request.TotalAmount);
        Assert.Equal(CurrencyCode.Parse("INR"), snapshot.Request.Currency);
        Assert.Equal(ProcurementCategory.Hardware, snapshot.Request.Category);
        Assert.Equal(Urgency.Urgent, snapshot.Request.Urgency);
        Assert.Equal(DataSensitivity.Internal, snapshot.Request.DataSensitivity);
        Assert.Equal(30, snapshot.Request.ItemCount);
        Assert.Equal(30, snapshot.Request.ExpectedDeliveryDays);
        Assert.True(snapshot.Request.HasBusinessJustification);
        Assert.Equal(DepartmentCode.Parse("ENG"), snapshot.Department.Code);
        Assert.Equal(250_000m, snapshot.Department.AutoApprovalLimit);
        Assert.False(snapshot.Supplier.IsApproved);
        Assert.Equal(SupplierOnboardingStatus.InProgress, snapshot.Supplier.OnboardingStatus);
        Assert.Equal(SupplierRiskRating.Medium, snapshot.Supplier.RiskRating);
        Assert.True(snapshot.Supplier.IsActive);
        Assert.True(snapshot.Derived.ContainsTechnologyPurchase);
        Assert.True(snapshot.Derived.RequiresUrgencyException);
    }

    [Fact]
    public void LowValueOfficeScenarioProducesSafeBoundaryFacts()
    {
        PurchaseRequest request = new PurchaseRequestBuilder()
            .WithMetadata(Metadata(
                Urgency.Normal,
                DataSensitivity.Public,
                _evaluationDate,
                null))
            .WithItem(
                quantity: 1,
                unitPrice: 100m,
                category: ProcurementCategory.OfficeSupplies)
            .Build();

        EvaluationFactSnapshot snapshot = EvaluationFactSnapshot.Create(
            request,
            new DepartmentBuilder().Build(),
            new SupplierBuilder().Build(),
            _evaluationDate);

        Assert.Equal(0, snapshot.Request.ExpectedDeliveryDays);
        Assert.False(snapshot.Request.HasBusinessJustification);
        Assert.True(snapshot.Supplier.IsApproved);
        Assert.False(snapshot.Derived.ContainsTechnologyPurchase);
        Assert.False(snapshot.Derived.RequiresUrgencyException);
    }

    [Theory]
    [InlineData(ProcurementCategory.Software, true)]
    [InlineData(ProcurementCategory.Hardware, true)]
    [InlineData(ProcurementCategory.CloudService, true)]
    [InlineData(ProcurementCategory.OfficeSupplies, false)]
    [InlineData(ProcurementCategory.ProfessionalServices, false)]
    [InlineData(ProcurementCategory.Travel, false)]
    [InlineData(ProcurementCategory.Facilities, false)]
    [InlineData(ProcurementCategory.Other, false)]
    public void TechnologyDerivationUsesControlledCategories(
        ProcurementCategory category,
        bool expected)
    {
        PurchaseRequest request = new PurchaseRequestBuilder()
            .WithItem(category: category)
            .Build();

        EvaluationFactSnapshot snapshot = Create(request);

        Assert.Equal(expected, snapshot.Derived.ContainsTechnologyPurchase);
    }

    [Theory]
    [InlineData(Urgency.Normal, false)]
    [InlineData(Urgency.Urgent, true)]
    [InlineData(Urgency.Emergency, true)]
    public void UrgencyDerivationUsesControlledUrgency(Urgency urgency, bool expected)
    {
        PurchaseRequest request = new PurchaseRequestBuilder()
            .WithMetadata(Metadata(
                urgency,
                DataSensitivity.Internal,
                new DateOnly(2026, 8, 31),
                null))
            .WithItem()
            .Build();

        EvaluationFactSnapshot snapshot = Create(request);

        Assert.Equal(expected, snapshot.Derived.RequiresUrgencyException);
    }

    [Fact]
    public void MixedLineCategoriesResolveToOtherWhileTechnologyRemainsVisible()
    {
        PurchaseRequest request = new PurchaseRequestBuilder()
            .WithItem(category: ProcurementCategory.OfficeSupplies)
            .WithItem(
                Guid.Parse("aaaaaaaa-aaaa-7aaa-8aaa-aaaaaaaaaabb"),
                category: ProcurementCategory.Software)
            .Build();

        EvaluationFactSnapshot snapshot = Create(request);

        Assert.Equal(ProcurementCategory.Other, snapshot.Request.Category);
        Assert.True(snapshot.Derived.ContainsTechnologyPurchase);
    }

    [Fact]
    public void ItemCountIsTotalQuantityAndOverflowFailsSafely()
    {
        PurchaseRequest request = new PurchaseRequestBuilder()
            .WithItem(quantity: int.MaxValue, unitPrice: 0.01m)
            .WithItem(
                Guid.Parse("aaaaaaaa-aaaa-7aaa-8aaa-aaaaaaaaaabc"),
                quantity: int.MaxValue,
                unitPrice: 0.01m)
            .Build();

        DomainRuleException exception = Assert.Throws<DomainRuleException>(
            () => Create(request));

        Assert.Equal(DomainErrorCodes.AmountOverflow, exception.Code);
    }

    [Fact]
    public void MissingItemsAndPastDeliveryFailWithoutSnapshot()
    {
        PurchaseRequest empty = new PurchaseRequestBuilder().Build();
        DomainRuleException missingItems = Assert.Throws<DomainRuleException>(() => Create(empty));
        Assert.Equal(DomainErrorCodes.InvalidState, missingItems.Code);

        PurchaseRequest pastDelivery = new PurchaseRequestBuilder()
            .WithMetadata(Metadata(
                Urgency.Normal,
                DataSensitivity.Internal,
                _evaluationDate.AddDays(-1),
                null))
            .WithItem()
            .Build();
        DomainRuleException invalidDate = Assert.Throws<DomainRuleException>(
            () => Create(pastDelivery));
        Assert.Equal(DomainErrorCodes.Validation, invalidDate.Code);
    }

    [Fact]
    public void MismatchedReferencesAreRejected()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().WithItem().Build();
        Department wrongDepartment = new DepartmentBuilder()
            .WithId(Guid.Parse("33333333-3333-7333-8333-333333333399"))
            .Build();
        Supplier wrongSupplier = new SupplierBuilder()
            .WithId(Guid.Parse("44444444-4444-7444-8444-444444444499"))
            .Build();

        DomainRuleException departmentMismatch = Assert.Throws<DomainRuleException>(
            () => EvaluationFactSnapshot.Create(
                request,
                wrongDepartment,
                new SupplierBuilder().Build(),
                _evaluationDate));
        DomainRuleException supplierMismatch = Assert.Throws<DomainRuleException>(
            () => EvaluationFactSnapshot.Create(
                request,
                new DepartmentBuilder().Build(),
                wrongSupplier,
                _evaluationDate));

        Assert.Equal(DomainErrorCodes.ReferenceMismatch, departmentMismatch.Code);
        Assert.Equal(DomainErrorCodes.ReferenceMismatch, supplierMismatch.Code);
    }

    [Fact]
    public void InactiveReferencesAreRejectedIndividually()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().WithItem().Build();
        Department inactiveDepartment = new DepartmentBuilder().Build();
        inactiveDepartment.SetActive(
            false,
            DepartmentBuilder.DefaultToken,
            DepartmentBuilder.NextToken,
            PurchaseRequestBuilder.DefaultTime.AddMinutes(1));
        Supplier inactiveSupplier = new SupplierBuilder().Build();
        inactiveSupplier.SetActive(
            false,
            SupplierBuilder.DefaultToken,
            SupplierBuilder.NextToken,
            PurchaseRequestBuilder.DefaultTime.AddMinutes(1));

        DomainRuleException departmentInactive = Assert.Throws<DomainRuleException>(
            () => EvaluationFactSnapshot.Create(
                request,
                inactiveDepartment,
                new SupplierBuilder().Build(),
                _evaluationDate));
        DomainRuleException supplierInactive = Assert.Throws<DomainRuleException>(
            () => EvaluationFactSnapshot.Create(
                request,
                new DepartmentBuilder().Build(),
                inactiveSupplier,
                _evaluationDate));

        Assert.Equal(DomainErrorCodes.InactiveReference, departmentInactive.Code);
        Assert.Equal(DomainErrorCodes.InactiveReference, supplierInactive.Code);
    }

    [Fact]
    public void DepartmentCurrencyMustMatchRequestCurrency()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().WithItem().Build();
        Department department = new DepartmentBuilder()
            .WithAutoApprovalLimit(Money.Create(1m, CurrencyCode.Parse("USD")))
            .Build();

        DomainRuleException exception = Assert.Throws<DomainRuleException>(
            () => EvaluationFactSnapshot.Create(
                request,
                department,
                new SupplierBuilder().Build(),
                _evaluationDate));

        Assert.Equal(DomainErrorCodes.CurrencyMismatch, exception.Code);
    }

    [Fact]
    public void NullInputsAreRejected()
    {
        PurchaseRequest request = new PurchaseRequestBuilder().WithItem().Build();
        Department department = new DepartmentBuilder().Build();
        Supplier supplier = new SupplierBuilder().Build();

        Assert.Throws<ArgumentNullException>(
            () => EvaluationFactSnapshot.Create(null!, department, supplier, _evaluationDate));
        Assert.Throws<ArgumentNullException>(
            () => EvaluationFactSnapshot.Create(request, null!, supplier, _evaluationDate));
        Assert.Throws<ArgumentNullException>(
            () => EvaluationFactSnapshot.Create(request, department, null!, _evaluationDate));
    }

    private static EvaluationFactSnapshot Create(PurchaseRequest request)
    {
        return EvaluationFactSnapshot.Create(
            request,
            new DepartmentBuilder().Build(),
            new SupplierBuilder().Build(),
            _evaluationDate);
    }

    private static PurchaseRequestMetadata Metadata(
        Urgency urgency,
        DataSensitivity sensitivity,
        DateOnly expectedDeliveryDate,
        BusinessJustification? justification)
    {
        return PurchaseRequestMetadata.Create(
            PurchaseRequestBuilder.DefaultDepartmentId,
            PurchaseRequestBuilder.DefaultSupplierId,
            urgency,
            sensitivity,
            expectedDeliveryDate,
            justification);
    }
}
