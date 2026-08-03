using DecisionForge.Application.PurchaseRequests;
using DecisionForge.Application.PurchaseRequests.Submission;
using DecisionForge.Domain.EvaluationFacts;
using DecisionForge.Domain.PurchaseRequests;

namespace DecisionForge.Application.Decisions;

public static class NormalizedEvaluationInputBuilder
{
    public static EvaluationFactSnapshot Build(
        PurchaseRequest purchaseRequest,
        SubmissionPreconditionResult validation,
        DateOnly evaluationDate)
    {
        ArgumentNullException.ThrowIfNull(purchaseRequest);
        ArgumentNullException.ThrowIfNull(validation);
        if (!validation.IsValid)
        {
            throw new SubmissionPreconditionException(validation.Errors);
        }

        EnsureSourcesPresent(validation);

        DepartmentEvaluationSource department = DepartmentEvaluationSource.Create(
            validation.Department!.Id,
            validation.Department.Code,
            validation.Department.AutoApprovalLimit,
            validation.Department.IsActive);
        SupplierEvaluationSource supplier = SupplierEvaluationSource.Create(
            validation.Supplier!.Id,
            validation.Supplier.ApprovalStatus,
            validation.Supplier.OnboardingStatus,
            validation.Supplier.RiskRating,
            validation.Supplier.IsActive);
        return EvaluationFactSnapshot.Create(
            purchaseRequest,
            department,
            supplier,
            evaluationDate);
    }

    private static void EnsureSourcesPresent(SubmissionPreconditionResult validation)
    {
        List<SubmissionPreconditionError> errors = [];
        if (validation.Department is null)
        {
            errors.Add(new SubmissionPreconditionError(
                PurchaseRequestApplicationErrorCodes.DepartmentNotFound,
                "metadata.departmentId",
                "The selected department does not exist."));
        }

        if (validation.Supplier is null)
        {
            errors.Add(new SubmissionPreconditionError(
                PurchaseRequestApplicationErrorCodes.SupplierNotFound,
                "metadata.supplierId",
                "The selected supplier does not exist."));
        }

        if (errors.Count > 0)
        {
            throw new SubmissionPreconditionException(errors);
        }
    }
}
