using DecisionForge.Domain.Enums;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.Approvals;

public sealed record ApproveApprovalStageCommand(
    Guid StageId,
    ConcurrencyToken ExpectedToken,
    string? Note);

public sealed record RejectApprovalStageCommand(
    Guid StageId,
    ConcurrencyToken ExpectedToken,
    string Reason);

public sealed record OverrideApprovalWorkflowCommand(
    Guid WorkflowId,
    ApprovalOutcome Outcome,
    ConcurrencyToken ExpectedCurrentStageToken,
    string Reason);
