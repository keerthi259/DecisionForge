using DecisionForge.Application.PurchaseRequests;
using DecisionForge.Application.PurchaseRequests.Submission;
using DecisionForge.Application.ReferenceData.Ports;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.PurchaseRequests;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.UnitTests.PurchaseRequests;

public sealed class PurchaseRequestSubmissionPreconditionValidatorTests
{
    [Fact]
    public void ConstructorRejectsNullDependencies()
    {
        StubDepartmentQueries departments = new();
        StubSupplierQueries suppliers = new();
        RequestFixedTimeProvider time = new(PurchaseRequestApplicationTestData.CurrentTime);

        Assert.Throws<ArgumentNullException>(
            () => new PurchaseRequestSubmissionPreconditionValidator(null!, suppliers, time));
        Assert.Throws<ArgumentNullException>(
            () => new PurchaseRequestSubmissionPreconditionValidator(departments, null!, time));
        Assert.Throws<ArgumentNullException>(
            () => new PurchaseRequestSubmissionPreconditionValidator(departments, suppliers, null!));
    }

    [Fact]
    public async Task ActiveReferencesAndAtLeastOneItemPassValidation()
    {
        StubDepartmentQueries departments = new() { Lookup = Department(isActive: true) };
        StubSupplierQueries suppliers = new() { Lookup = Supplier(isActive: true) };
        PurchaseRequestSubmissionPreconditionValidator validator = CreateValidator(departments, suppliers);

        SubmissionPreconditionResult result = await validator.ValidateAsync(
            PurchaseRequestApplicationTestData.CreateRequest(withItem: true),
            CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal(1, departments.FindCalls);
        Assert.Equal(1, suppliers.FindCalls);
    }

    [Fact]
    public async Task MissingItemsAndReferencesReturnAllControlledErrors()
    {
        PurchaseRequestSubmissionPreconditionValidator validator = new(
            new StubDepartmentQueries(),
            new StubSupplierQueries(),
            new RequestFixedTimeProvider(PurchaseRequestApplicationTestData.CurrentTime));

        SubmissionPreconditionResult result = await validator.ValidateAsync(
            PurchaseRequestApplicationTestData.CreateRequest(),
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(
            [
                PurchaseRequestApplicationErrorCodes.ItemsRequired,
                PurchaseRequestApplicationErrorCodes.DepartmentNotFound,
                PurchaseRequestApplicationErrorCodes.SupplierNotFound,
            ],
            result.Errors.Select(error => error.Code));
        Assert.Equal(
            ["items", "metadata.departmentId", "metadata.supplierId"],
            result.Errors.Select(error => error.Path));
    }

    [Fact]
    public async Task InactiveReferencesAndCurrencyMismatchAreExplainedIndependently()
    {
        StubDepartmentQueries departments = new()
        {
            Lookup = Department(isActive: false, currency: "USD"),
        };
        StubSupplierQueries suppliers = new() { Lookup = Supplier(isActive: false) };
        PurchaseRequestSubmissionPreconditionValidator validator = CreateValidator(departments, suppliers);

        SubmissionPreconditionResult result = await validator.ValidateAsync(
            PurchaseRequestApplicationTestData.CreateRequest(withItem: true),
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(
            [
                PurchaseRequestApplicationErrorCodes.DepartmentInactive,
                PurchaseRequestApplicationErrorCodes.DepartmentCurrencyMismatch,
                PurchaseRequestApplicationErrorCodes.SupplierInactive,
            ],
            result.Errors.Select(error => error.Code));
        Assert.All(result.Errors, error => Assert.DoesNotContain(
            PurchaseRequestApplicationTestData.DepartmentId.ToString(),
            error.Message,
            StringComparison.Ordinal));
    }

    [Fact]
    public async Task NonDraftAndPastDeliveryDateAreRejectedBeforeOrchestration()
    {
        PurchaseRequestMetadata metadata = PurchaseRequestMetadata.Create(
            PurchaseRequestApplicationTestData.DepartmentId,
            PurchaseRequestApplicationTestData.SupplierId,
            Urgency.Normal,
            DataSensitivity.Internal,
            new DateOnly(2026, 8, 2),
            null);
        PurchaseRequest request = PurchaseRequest.Create(
            PurchaseRequestApplicationTestData.RequestId,
            RequestNumber.Parse("PR-2026-000001"),
            PurchaseRequestApplicationTestData.RequesterId,
            PurchaseRequestApplicationTestData.Currency,
            metadata,
            PurchaseRequestApplicationTestData.Token(0),
            PurchaseRequestApplicationTestData.InitialTime);
        _ = request.AddItem(
            PurchaseRequestApplicationTestData.ItemId(1),
            "Cable",
            1,
            Money.Create(10m, request.Currency),
            ProcurementCategory.Hardware,
            request.ConcurrencyToken,
            PurchaseRequestApplicationTestData.Token(1),
            PurchaseRequestApplicationTestData.InitialTime);
        request.Submit(
            request.ConcurrencyToken,
            PurchaseRequestApplicationTestData.Token(2),
            PurchaseRequestApplicationTestData.CurrentTime);
        StubDepartmentQueries departments = new() { Lookup = Department(isActive: true) };
        StubSupplierQueries suppliers = new() { Lookup = Supplier(isActive: true) };

        SubmissionPreconditionResult result = await CreateValidator(departments, suppliers)
            .ValidateAsync(request, CancellationToken.None);

        Assert.Equal(
            [
                PurchaseRequestApplicationErrorCodes.SubmissionInvalidState,
                PurchaseRequestApplicationErrorCodes.ExpectedDeliveryDatePast,
            ],
            result.Errors.Select(error => error.Code));
    }

    [Fact]
    public async Task ResultErrorsAreImmutableSnapshots()
    {
        StubDepartmentQueries departments = new();
        StubSupplierQueries suppliers = new();
        PurchaseRequestSubmissionPreconditionValidator validator = CreateValidator(departments, suppliers);
        SubmissionPreconditionResult result = await validator.ValidateAsync(
            PurchaseRequestApplicationTestData.CreateRequest(),
            CancellationToken.None);
        ICollection<SubmissionPreconditionError> exposed =
            Assert.IsAssignableFrom<ICollection<SubmissionPreconditionError>>(result.Errors);

        Assert.True(exposed.IsReadOnly);
        Assert.Throws<NotSupportedException>(exposed.Clear);
    }

    [Fact]
    public async Task CancellationAndNullRequestFailBeforeQueryingReferences()
    {
        StubDepartmentQueries departments = new() { Lookup = Department(isActive: true) };
        StubSupplierQueries suppliers = new() { Lookup = Supplier(isActive: true) };
        PurchaseRequestSubmissionPreconditionValidator validator = CreateValidator(departments, suppliers);
        using CancellationTokenSource source = new();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => validator.ValidateAsync(
                PurchaseRequestApplicationTestData.CreateRequest(withItem: true),
                source.Token));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => validator.ValidateAsync(null!, CancellationToken.None));

        Assert.Equal(0, departments.FindCalls);
        Assert.Equal(0, suppliers.FindCalls);
    }

    private static DepartmentLookup Department(bool isActive, string currency = "INR")
    {
        return new DepartmentLookup(
            PurchaseRequestApplicationTestData.DepartmentId,
            DepartmentCode.Parse("ENG"),
            "Engineering",
            Money.Create(250_000m, CurrencyCode.Parse(currency)),
            isActive);
    }

    private static PurchaseRequestSubmissionPreconditionValidator CreateValidator(
        StubDepartmentQueries departments,
        StubSupplierQueries suppliers)
    {
        return new PurchaseRequestSubmissionPreconditionValidator(
            departments,
            suppliers,
            new RequestFixedTimeProvider(PurchaseRequestApplicationTestData.CurrentTime));
    }

    private static SupplierLookup Supplier(bool isActive)
    {
        return new SupplierLookup(
            PurchaseRequestApplicationTestData.SupplierId,
            SupplierRegistrationNumber.Parse("SUP-001"),
            "Global Technology Systems",
            SupplierApprovalStatus.Approved,
            SupplierOnboardingStatus.Completed,
            SupplierRiskRating.Medium,
            isActive);
    }
}
