using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.ReferenceData.Departments;

public sealed record CreateDepartmentCommand(
    DepartmentCode Code,
    string Name,
    Money AutoApprovalLimit);

public sealed record UpdateDepartmentCommand(
    Guid DepartmentId,
    string Name,
    Money AutoApprovalLimit,
    ConcurrencyToken ExpectedToken);

public sealed record SetDepartmentActiveCommand(
    Guid DepartmentId,
    bool IsActive,
    ConcurrencyToken ExpectedToken);
