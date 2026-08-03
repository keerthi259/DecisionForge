using DecisionForge.Domain.Approvals;
using DecisionForge.Domain.PurchaseRequests;

namespace DecisionForge.Application.Approvals;

public sealed class ApprovalActionState
{
    public ApprovalActionState(
        ApprovalWorkflow workflow,
        PurchaseRequest purchaseRequest)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(purchaseRequest);
        if (workflow.PurchaseRequestId != purchaseRequest.Id)
        {
            throw new ArgumentException(
                "The approval workflow and purchase request must identify the same request.",
                nameof(purchaseRequest));
        }

        Workflow = workflow;
        PurchaseRequest = purchaseRequest;
    }

    public ApprovalWorkflow Workflow { get; }

    public PurchaseRequest PurchaseRequest { get; }
}

public sealed record ApprovalActionResult(
    ApprovalWorkflow Workflow,
    PurchaseRequest PurchaseRequest);
