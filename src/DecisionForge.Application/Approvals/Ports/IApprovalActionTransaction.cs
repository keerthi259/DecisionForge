using DecisionForge.Domain.Approvals;
using DecisionForge.Domain.PurchaseRequests;

namespace DecisionForge.Application.Approvals.Ports;

public interface IApprovalActionTransaction
{
    Task<ApprovalActionState?> FindByStageIdAsync(
        Guid stageId,
        CancellationToken cancellationToken);

    Task<ApprovalActionState?> FindByWorkflowIdAsync(
        Guid workflowId,
        CancellationToken cancellationToken);

    Task CommitAsync(
        ApprovalWorkflow workflow,
        PurchaseRequest purchaseRequest,
        CancellationToken cancellationToken);
}
