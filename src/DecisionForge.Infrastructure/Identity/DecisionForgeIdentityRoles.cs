using DecisionForge.Domain.Policies;

namespace DecisionForge.Infrastructure.Identity;

public static class DecisionForgeIdentityRoles
{
    public const string Requester = "Requester";
    public const string DepartmentApprover = "DepartmentApprover";
    public const string ProcurementApprover = "ProcurementApprover";
    public const string SecurityApprover = "SecurityApprover";
    public const string FinanceApprover = "FinanceApprover";
    public const string SeniorApprover = "SeniorApprover";
    public const string PolicyAuthor = "PolicyAuthor";
    public const string PolicyPublisher = "PolicyPublisher";
    public const string Auditor = "Auditor";
    public const string Administrator = "Administrator";

    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(
        new[]
        {
            Requester,
            DepartmentApprover,
            ProcurementApprover,
            SecurityApprover,
            FinanceApprover,
            SeniorApprover,
            PolicyAuthor,
            PolicyPublisher,
            Auditor,
            Administrator,
        });

    public static bool TryGetApproverRole(string roleName, out PolicyApproverRole role)
    {
        return Enum.TryParse(roleName, ignoreCase: false, out role)
            && Enum.IsDefined(role);
    }
}
