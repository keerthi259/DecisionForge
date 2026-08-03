using DecisionForge.Application.ReferenceData.Ports;
using DecisionForge.Domain.Enums;
using DecisionForge.Domain.PurchaseRequests;

namespace DecisionForge.Application.PurchaseRequests.Submission;

public sealed class PurchaseRequestSubmissionPreconditionValidator
{
    private readonly IDepartmentQueries _departmentQueries;
    private readonly ISupplierQueries _supplierQueries;
    private readonly TimeProvider _timeProvider;

    public PurchaseRequestSubmissionPreconditionValidator(
        IDepartmentQueries departmentQueries,
        ISupplierQueries supplierQueries,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(departmentQueries);
        ArgumentNullException.ThrowIfNull(supplierQueries);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _departmentQueries = departmentQueries;
        _supplierQueries = supplierQueries;
        _timeProvider = timeProvider;
    }

    public async Task<SubmissionPreconditionResult> ValidateAsync(
        PurchaseRequest purchaseRequest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(purchaseRequest);
        cancellationToken.ThrowIfCancellationRequested();
        List<SubmissionPreconditionError> errors = [];
        if (purchaseRequest.Status != PurchaseRequestStatus.Draft)
        {
            errors.Add(Error(
                PurchaseRequestApplicationErrorCodes.SubmissionInvalidState,
                "status",
                "Only a draft purchase request can be submitted."));
        }

        if (purchaseRequest.Items.Count == 0)
        {
            errors.Add(Error(
                PurchaseRequestApplicationErrorCodes.ItemsRequired,
                "items",
                "At least one item is required before submission."));
        }

        DateOnly currentDate = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        if (purchaseRequest.Metadata.ExpectedDeliveryDate < currentDate)
        {
            errors.Add(Error(
                PurchaseRequestApplicationErrorCodes.ExpectedDeliveryDatePast,
                "metadata.expectedDeliveryDate",
                "Expected delivery date cannot be in the past."));
        }

        DepartmentLookup? department = await _departmentQueries.FindByIdAsync(
            purchaseRequest.Metadata.DepartmentId,
            cancellationToken);
        AddDepartmentErrors(purchaseRequest, department, errors);
        SupplierLookup? supplier = await _supplierQueries.FindByIdAsync(
            purchaseRequest.Metadata.SupplierId,
            cancellationToken);
        AddSupplierErrors(supplier, errors);
        return new SubmissionPreconditionResult(errors, department, supplier);
    }

    private static void AddDepartmentErrors(
        PurchaseRequest request,
        DepartmentLookup? department,
        List<SubmissionPreconditionError> errors)
    {
        if (department is null)
        {
            errors.Add(Error(
                PurchaseRequestApplicationErrorCodes.DepartmentNotFound,
                "metadata.departmentId",
                "The selected department does not exist."));
            return;
        }

        if (!department.IsActive)
        {
            errors.Add(Error(
                PurchaseRequestApplicationErrorCodes.DepartmentInactive,
                "metadata.departmentId",
                "The selected department is inactive."));
        }

        if (department.AutoApprovalLimit.Currency != request.Currency)
        {
            errors.Add(Error(
                PurchaseRequestApplicationErrorCodes.DepartmentCurrencyMismatch,
                "metadata.departmentId",
                "The department threshold currency does not match the purchase request."));
        }
    }

    private static void AddSupplierErrors(
        SupplierLookup? supplier,
        List<SubmissionPreconditionError> errors)
    {
        if (supplier is null)
        {
            errors.Add(Error(
                PurchaseRequestApplicationErrorCodes.SupplierNotFound,
                "metadata.supplierId",
                "The selected supplier does not exist."));
            return;
        }

        if (!supplier.IsActive)
        {
            errors.Add(Error(
                PurchaseRequestApplicationErrorCodes.SupplierInactive,
                "metadata.supplierId",
                "The selected supplier is inactive."));
        }
    }

    private static SubmissionPreconditionError Error(
        string code,
        string path,
        string message)
    {
        return new SubmissionPreconditionError(code, path, message);
    }
}
