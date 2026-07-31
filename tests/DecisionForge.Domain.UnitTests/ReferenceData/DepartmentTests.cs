using DecisionForge.Domain.Common;
using DecisionForge.Domain.ReferenceData;
using DecisionForge.Domain.ReferenceData.Events;
using DecisionForge.Domain.UnitTests.Builders;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.UnitTests.ReferenceData;

public sealed class DepartmentTests
{
    private static readonly DateTimeOffset _later = PurchaseRequestBuilder.DefaultTime.AddMinutes(1);

    [Fact]
    public void CreateProducesActiveDeterministicDepartmentAndEvent()
    {
        Department department = new DepartmentBuilder().Build();

        Assert.Equal(DepartmentBuilder.DefaultId, department.Id);
        Assert.Equal("ENG", department.Code.Value);
        Assert.Equal("Engineering", department.Name);
        Assert.Equal(250_000m, department.AutoApprovalLimit.Amount);
        Assert.True(department.IsActive);
        Assert.Equal(DepartmentBuilder.DefaultToken, department.ConcurrencyToken);
        Assert.Equal(PurchaseRequestBuilder.DefaultTime, department.CreatedAt);
        Assert.Equal(PurchaseRequestBuilder.DefaultTime, department.LastModifiedAt);
        DepartmentCreatedDomainEvent created =
            Assert.IsType<DepartmentCreatedDomainEvent>(Assert.Single(department.DomainEvents));
        Assert.Equal(department.Id, created.DepartmentId);
        Assert.Equal(department.Code, created.Code);
        Assert.Equal(department.CreatedAt, created.OccurredAt);
    }

    [Fact]
    public void CreateAllowsAutoApprovalLimitBoundaries()
    {
        Department zero = new DepartmentBuilder()
            .WithAutoApprovalLimit(Money.Zero(CurrencyCode.Parse("INR")))
            .Build();
        Department maximum = new DepartmentBuilder()
            .WithAutoApprovalLimit(Money.Create(
                Money.MaximumAmount,
                CurrencyCode.Parse("INR")))
            .Build();

        Assert.Equal(0m, zero.AutoApprovalLimit.Amount);
        Assert.Equal(Money.MaximumAmount, maximum.AutoApprovalLimit.Amount);
    }

    [Fact]
    public void CreateRejectsInvalidIdentityNameDependenciesAndTime()
    {
        AssertValidation(() => new DepartmentBuilder().WithId(Guid.Empty).Build());
        AssertValidation(() => new DepartmentBuilder().WithName(" ").Build());
        AssertValidation(
            () => new DepartmentBuilder()
                .WithName(new string('x', 201))
                .Build());
        Assert.Throws<ArgumentNullException>(
            () => Department.Create(
                DepartmentBuilder.DefaultId,
                null!,
                "Engineering",
                Money.Create(1m, CurrencyCode.Parse("INR")),
                DepartmentBuilder.DefaultToken,
                PurchaseRequestBuilder.DefaultTime));
        Assert.Throws<ArgumentNullException>(
            () => Department.Create(
                DepartmentBuilder.DefaultId,
                DepartmentCode.Parse("ENG"),
                "Engineering",
                null!,
                DepartmentBuilder.DefaultToken,
                PurchaseRequestBuilder.DefaultTime));
        Assert.Throws<ArgumentNullException>(
            () => Department.Create(
                DepartmentBuilder.DefaultId,
                DepartmentCode.Parse("ENG"),
                "Engineering",
                Money.Create(1m, CurrencyCode.Parse("INR")),
                null!,
                PurchaseRequestBuilder.DefaultTime));
        AssertValidation(
            () => Department.Create(
                DepartmentBuilder.DefaultId,
                DepartmentCode.Parse("ENG"),
                "Engineering",
                Money.Create(1m, CurrencyCode.Parse("INR")),
                DepartmentBuilder.DefaultToken,
                PurchaseRequestBuilder.DefaultTime.ToOffset(TimeSpan.FromHours(1))));
    }

    [Fact]
    public void UpdateDetailsRotatesTokenAndRaisesControlledEvent()
    {
        Department department = new DepartmentBuilder().Build();
        department.ClearDomainEvents();
        Money threshold = Money.Create(300_000m, CurrencyCode.Parse("INR"));

        department.UpdateDetails(
            "  Product Engineering  ",
            threshold,
            DepartmentBuilder.DefaultToken,
            DepartmentBuilder.NextToken,
            _later);

        Assert.Equal("Product Engineering", department.Name);
        Assert.Equal(threshold, department.AutoApprovalLimit);
        Assert.Equal(DepartmentBuilder.NextToken, department.ConcurrencyToken);
        Assert.Equal(_later, department.LastModifiedAt);
        DepartmentDetailsChangedDomainEvent changed =
            Assert.IsType<DepartmentDetailsChangedDomainEvent>(
                Assert.Single(department.DomainEvents));
        Assert.Equal(threshold, changed.AutoApprovalLimit);
        Assert.Equal(_later, changed.OccurredAt);
        Assert.DoesNotContain("Product Engineering", changed.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void IdenticalDetailsAreNoOp()
    {
        Department department = new DepartmentBuilder().Build();
        department.ClearDomainEvents();

        department.UpdateDetails(
            "Engineering",
            department.AutoApprovalLimit,
            DepartmentBuilder.DefaultToken,
            DepartmentBuilder.NextToken,
            _later);

        Assert.Equal(DepartmentBuilder.DefaultToken, department.ConcurrencyToken);
        Assert.Equal(PurchaseRequestBuilder.DefaultTime, department.LastModifiedAt);
        Assert.Empty(department.DomainEvents);
    }

    [Fact]
    public void ConcurrencyAndMutationValidationLeaveDetailsUnchanged()
    {
        Department department = new DepartmentBuilder().Build();
        Money originalThreshold = department.AutoApprovalLimit;
        department.ClearDomainEvents();

        DomainRuleException stale = Assert.Throws<DomainRuleException>(
            () => department.UpdateDetails(
                "Changed",
                Money.Create(1m, CurrencyCode.Parse("INR")),
                DepartmentBuilder.NextToken,
                ConcurrencyToken.Create(Guid.Parse("55555555-5555-7555-8555-555555555557")),
                _later));
        Assert.Equal(DomainErrorCodes.ConcurrencyConflict, stale.Code);
        AssertValidation(
            () => department.UpdateDetails(
                "Changed",
                Money.Create(1m, CurrencyCode.Parse("INR")),
                DepartmentBuilder.DefaultToken,
                DepartmentBuilder.DefaultToken,
                _later));
        AssertValidation(
            () => department.UpdateDetails(
                "Changed",
                Money.Create(1m, CurrencyCode.Parse("INR")),
                DepartmentBuilder.DefaultToken,
                DepartmentBuilder.NextToken,
                PurchaseRequestBuilder.DefaultTime.AddTicks(-1)));
        AssertValidation(
            () => department.UpdateDetails(
                " ",
                Money.Create(1m, CurrencyCode.Parse("INR")),
                DepartmentBuilder.DefaultToken,
                DepartmentBuilder.NextToken,
                _later));
        Assert.Throws<ArgumentNullException>(
            () => department.UpdateDetails(
                "Changed",
                null!,
                DepartmentBuilder.DefaultToken,
                DepartmentBuilder.NextToken,
                _later));

        Assert.Equal("Engineering", department.Name);
        Assert.Equal(originalThreshold, department.AutoApprovalLimit);
        Assert.Equal(DepartmentBuilder.DefaultToken, department.ConcurrencyToken);
        Assert.Empty(department.DomainEvents);
    }

    [Fact]
    public void ActivationTransitionsAreExplicitConcurrencyProtectedAndAuditable()
    {
        Department department = new DepartmentBuilder().Build();
        department.ClearDomainEvents();

        department.SetActive(
            false,
            DepartmentBuilder.DefaultToken,
            DepartmentBuilder.NextToken,
            _later);

        Assert.False(department.IsActive);
        DepartmentActivationChangedDomainEvent deactivated =
            Assert.IsType<DepartmentActivationChangedDomainEvent>(
                Assert.Single(department.DomainEvents));
        Assert.False(deactivated.IsActive);

        ConcurrencyToken thirdToken = ConcurrencyToken.Create(
            Guid.Parse("55555555-5555-7555-8555-555555555557"));
        department.SetActive(true, DepartmentBuilder.NextToken, thirdToken, _later.AddMinutes(1));
        Assert.True(department.IsActive);
        Assert.True(
            Assert.IsType<DepartmentActivationChangedDomainEvent>(department.DomainEvents[1]).IsActive);

        DomainRuleException repeated = Assert.Throws<DomainRuleException>(
            () => department.SetActive(true, thirdToken, DepartmentBuilder.DefaultToken, _later.AddMinutes(2)));
        Assert.Equal(DomainErrorCodes.InvalidState, repeated.Code);
    }

    private static void AssertValidation(Action action)
    {
        DomainRuleException exception = Assert.Throws<DomainRuleException>(action);
        Assert.Equal(DomainErrorCodes.Validation, exception.Code);
    }
}
