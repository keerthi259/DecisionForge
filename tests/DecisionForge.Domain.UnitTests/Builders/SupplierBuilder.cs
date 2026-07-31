using DecisionForge.Domain.Enums;
using DecisionForge.Domain.ReferenceData;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.UnitTests.Builders;

internal sealed class SupplierBuilder
{
    public static readonly Guid DefaultId = PurchaseRequestBuilder.DefaultSupplierId;
    public static readonly ConcurrencyToken DefaultToken = ConcurrencyToken.Create(
        Guid.Parse("66666666-6666-7666-8666-666666666666"));
    public static readonly ConcurrencyToken NextToken = ConcurrencyToken.Create(
        Guid.Parse("66666666-6666-7666-8666-666666666667"));

    private Guid _id = DefaultId;
    private string _name = "Global Technology Systems";
    private SupplierApprovalStatus _approvalStatus = SupplierApprovalStatus.Approved;
    private SupplierOnboardingStatus _onboardingStatus = SupplierOnboardingStatus.Completed;
    private SupplierRiskRating _riskRating = SupplierRiskRating.Medium;

    public SupplierBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public SupplierBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public SupplierBuilder WithStates(
        SupplierApprovalStatus approvalStatus,
        SupplierOnboardingStatus onboardingStatus,
        SupplierRiskRating riskRating)
    {
        _approvalStatus = approvalStatus;
        _onboardingStatus = onboardingStatus;
        _riskRating = riskRating;
        return this;
    }

    public Supplier Build()
    {
        return Supplier.Create(
            _id,
            SupplierRegistrationNumber.Parse("IN-KA-12345"),
            _name,
            _approvalStatus,
            _onboardingStatus,
            _riskRating,
            DefaultToken,
            PurchaseRequestBuilder.DefaultTime);
    }
}
