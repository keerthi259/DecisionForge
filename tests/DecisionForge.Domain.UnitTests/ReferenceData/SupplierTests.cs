using DecisionForge.Domain.Common;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.ReferenceData;
using DecisionForge.Domain.ReferenceData.Events;
using DecisionForge.Domain.UnitTests.Builders;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.UnitTests.ReferenceData;

public sealed class SupplierTests
{
    private static readonly DateTimeOffset _later = PurchaseRequestBuilder.DefaultTime.AddMinutes(1);

    [Fact]
    public void CreateProducesActiveDeterministicSupplierAndEvent()
    {
        Supplier supplier = new SupplierBuilder().Build();

        Assert.Equal(SupplierBuilder.DefaultId, supplier.Id);
        Assert.Equal("IN-KA-12345", supplier.RegistrationNumber.Value);
        Assert.Equal("Global Technology Systems", supplier.Name);
        Assert.Equal(SupplierApprovalStatus.Approved, supplier.ApprovalStatus);
        Assert.Equal(SupplierOnboardingStatus.Completed, supplier.OnboardingStatus);
        Assert.Equal(SupplierRiskRating.Medium, supplier.RiskRating);
        Assert.True(supplier.IsActive);
        Assert.Equal(SupplierBuilder.DefaultToken, supplier.ConcurrencyToken);
        Assert.Equal(PurchaseRequestBuilder.DefaultTime, supplier.CreatedAt);
        SupplierCreatedDomainEvent created =
            Assert.IsType<SupplierCreatedDomainEvent>(Assert.Single(supplier.DomainEvents));
        Assert.Equal(supplier.Id, created.SupplierId);
        Assert.Equal(supplier.RegistrationNumber, created.RegistrationNumber);
    }

    [Fact]
    public void EveryControlledSupplierStateCanBeCreated()
    {
        foreach (SupplierApprovalStatus approval in Enum.GetValues<SupplierApprovalStatus>())
        {
            foreach (SupplierOnboardingStatus onboarding in Enum.GetValues<SupplierOnboardingStatus>())
            {
                foreach (SupplierRiskRating risk in Enum.GetValues<SupplierRiskRating>())
                {
                    Supplier supplier = new SupplierBuilder()
                        .WithStates(approval, onboarding, risk)
                        .Build();
                    Assert.Equal(approval, supplier.ApprovalStatus);
                    Assert.Equal(onboarding, supplier.OnboardingStatus);
                    Assert.Equal(risk, supplier.RiskRating);
                }
            }
        }
    }

    [Fact]
    public void CreateRejectsInvalidIdentityNameDependenciesTimeAndEnums()
    {
        AssertValidation(() => new SupplierBuilder().WithId(Guid.Empty).Build());
        AssertValidation(() => new SupplierBuilder().WithName(" ").Build());
        AssertValidation(() => new SupplierBuilder().WithName(new string('x', 201)).Build());
        Assert.Throws<ArgumentNullException>(
            () => Supplier.Create(
                SupplierBuilder.DefaultId,
                null!,
                "Supplier",
                SupplierApprovalStatus.Pending,
                SupplierOnboardingStatus.NotStarted,
                SupplierRiskRating.Low,
                SupplierBuilder.DefaultToken,
                PurchaseRequestBuilder.DefaultTime));
        Assert.Throws<ArgumentNullException>(
            () => Supplier.Create(
                SupplierBuilder.DefaultId,
                SupplierRegistrationNumber.Parse("REG-1"),
                "Supplier",
                SupplierApprovalStatus.Pending,
                SupplierOnboardingStatus.NotStarted,
                SupplierRiskRating.Low,
                null!,
                PurchaseRequestBuilder.DefaultTime));
        AssertValidation(
            () => new SupplierBuilder()
                .WithStates((SupplierApprovalStatus)999, SupplierOnboardingStatus.NotStarted, SupplierRiskRating.Low)
                .Build());
        AssertValidation(
            () => new SupplierBuilder()
                .WithStates(SupplierApprovalStatus.Pending, (SupplierOnboardingStatus)999, SupplierRiskRating.Low)
                .Build());
        AssertValidation(
            () => new SupplierBuilder()
                .WithStates(SupplierApprovalStatus.Pending, SupplierOnboardingStatus.NotStarted, (SupplierRiskRating)999)
                .Build());
        AssertValidation(
            () => Supplier.Create(
                SupplierBuilder.DefaultId,
                SupplierRegistrationNumber.Parse("REG-1"),
                "Supplier",
                SupplierApprovalStatus.Pending,
                SupplierOnboardingStatus.NotStarted,
                SupplierRiskRating.Low,
                SupplierBuilder.DefaultToken,
                PurchaseRequestBuilder.DefaultTime.ToOffset(TimeSpan.FromHours(1))));
    }

    [Fact]
    public void UpdateDetailsRotatesTokenAndRaisesControlledEvent()
    {
        Supplier supplier = new SupplierBuilder().Build();
        supplier.ClearDomainEvents();

        supplier.UpdateDetails(
            "  Global Technology Systems India  ",
            SupplierApprovalStatus.Suspended,
            SupplierOnboardingStatus.Suspended,
            SupplierRiskRating.High,
            SupplierBuilder.DefaultToken,
            SupplierBuilder.NextToken,
            _later);

        Assert.Equal("Global Technology Systems India", supplier.Name);
        Assert.Equal(SupplierApprovalStatus.Suspended, supplier.ApprovalStatus);
        Assert.Equal(SupplierOnboardingStatus.Suspended, supplier.OnboardingStatus);
        Assert.Equal(SupplierRiskRating.High, supplier.RiskRating);
        Assert.Equal(SupplierBuilder.NextToken, supplier.ConcurrencyToken);
        SupplierDetailsChangedDomainEvent changed =
            Assert.IsType<SupplierDetailsChangedDomainEvent>(Assert.Single(supplier.DomainEvents));
        Assert.Equal(SupplierApprovalStatus.Suspended, changed.ApprovalStatus);
        Assert.Equal(SupplierOnboardingStatus.Suspended, changed.OnboardingStatus);
        Assert.Equal(SupplierRiskRating.High, changed.RiskRating);
        Assert.DoesNotContain("Global Technology", changed.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void IdenticalDetailsAreNoOp()
    {
        Supplier supplier = new SupplierBuilder().Build();
        supplier.ClearDomainEvents();

        supplier.UpdateDetails(
            supplier.Name,
            supplier.ApprovalStatus,
            supplier.OnboardingStatus,
            supplier.RiskRating,
            SupplierBuilder.DefaultToken,
            SupplierBuilder.NextToken,
            _later);

        Assert.Equal(SupplierBuilder.DefaultToken, supplier.ConcurrencyToken);
        Assert.Equal(PurchaseRequestBuilder.DefaultTime, supplier.LastModifiedAt);
        Assert.Empty(supplier.DomainEvents);
    }

    [Fact]
    public void InvalidUpdateLeavesSupplierUnchanged()
    {
        Supplier supplier = new SupplierBuilder().Build();
        supplier.ClearDomainEvents();

        DomainRuleException stale = Assert.Throws<DomainRuleException>(
            () => supplier.UpdateDetails(
                "Changed",
                SupplierApprovalStatus.Pending,
                SupplierOnboardingStatus.InProgress,
                SupplierRiskRating.High,
                SupplierBuilder.NextToken,
                ConcurrencyToken.Create(Guid.Parse("66666666-6666-7666-8666-666666666668")),
                _later));
        Assert.Equal(DomainErrorCodes.ConcurrencyConflict, stale.Code);
        AssertValidation(
            () => supplier.UpdateDetails(
                "Changed",
                SupplierApprovalStatus.Pending,
                SupplierOnboardingStatus.InProgress,
                SupplierRiskRating.High,
                SupplierBuilder.DefaultToken,
                SupplierBuilder.DefaultToken,
                _later));
        AssertValidation(
            () => supplier.UpdateDetails(
                "Changed",
                SupplierApprovalStatus.Pending,
                SupplierOnboardingStatus.InProgress,
                SupplierRiskRating.High,
                SupplierBuilder.DefaultToken,
                SupplierBuilder.NextToken,
                PurchaseRequestBuilder.DefaultTime.AddTicks(-1)));
        AssertValidation(
            () => supplier.UpdateDetails(
                "Changed",
                (SupplierApprovalStatus)999,
                SupplierOnboardingStatus.InProgress,
                SupplierRiskRating.High,
                SupplierBuilder.DefaultToken,
                SupplierBuilder.NextToken,
                _later));

        Assert.Equal("Global Technology Systems", supplier.Name);
        Assert.Equal(SupplierApprovalStatus.Approved, supplier.ApprovalStatus);
        Assert.Equal(SupplierBuilder.DefaultToken, supplier.ConcurrencyToken);
        Assert.Empty(supplier.DomainEvents);
    }

    [Fact]
    public void ActivationTransitionsAreExplicitConcurrencyProtectedAndAuditable()
    {
        Supplier supplier = new SupplierBuilder().Build();
        supplier.ClearDomainEvents();

        supplier.SetActive(
            false,
            SupplierBuilder.DefaultToken,
            SupplierBuilder.NextToken,
            _later);

        Assert.False(supplier.IsActive);
        SupplierActivationChangedDomainEvent deactivated =
            Assert.IsType<SupplierActivationChangedDomainEvent>(
                Assert.Single(supplier.DomainEvents));
        Assert.False(deactivated.IsActive);

        ConcurrencyToken thirdToken = ConcurrencyToken.Create(
            Guid.Parse("66666666-6666-7666-8666-666666666668"));
        supplier.SetActive(true, SupplierBuilder.NextToken, thirdToken, _later.AddMinutes(1));
        Assert.True(supplier.IsActive);
        Assert.True(
            Assert.IsType<SupplierActivationChangedDomainEvent>(supplier.DomainEvents[1]).IsActive);

        DomainRuleException repeated = Assert.Throws<DomainRuleException>(
            () => supplier.SetActive(true, thirdToken, SupplierBuilder.DefaultToken, _later.AddMinutes(2)));
        Assert.Equal(DomainErrorCodes.InvalidState, repeated.Code);
    }

    private static void AssertValidation(Action action)
    {
        DomainRuleException exception = Assert.Throws<DomainRuleException>(action);
        Assert.Equal(DomainErrorCodes.Validation, exception.Code);
    }
}
