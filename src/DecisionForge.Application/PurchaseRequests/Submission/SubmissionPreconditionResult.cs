using System.Collections.ObjectModel;
using DecisionForge.Application.ReferenceData.Ports;

namespace DecisionForge.Application.PurchaseRequests.Submission;

public sealed class SubmissionPreconditionError
{
    public SubmissionPreconditionError(string code, string path, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code;
        Path = path;
        Message = message;
    }

    public string Code { get; }

    public string Path { get; }

    public string Message { get; }
}

public sealed class SubmissionPreconditionResult
{
    private readonly ReadOnlyCollection<SubmissionPreconditionError> _errors;

    public SubmissionPreconditionResult(
        IReadOnlyCollection<SubmissionPreconditionError> errors,
        DepartmentLookup? department = null,
        SupplierLookup? supplier = null)
    {
        ArgumentNullException.ThrowIfNull(errors);
        _errors = Array.AsReadOnly(errors.ToArray());
        Department = department;
        Supplier = supplier;
    }

    public bool IsValid => _errors.Count == 0;

    public IReadOnlyList<SubmissionPreconditionError> Errors => _errors;

    public DepartmentLookup? Department { get; }

    public SupplierLookup? Supplier { get; }
}

public sealed class SubmissionPreconditionException : Exception
{
    private readonly ReadOnlyCollection<SubmissionPreconditionError> _errors;

    public SubmissionPreconditionException(
        IReadOnlyCollection<SubmissionPreconditionError> errors)
        : base("Purchase-request submission preconditions were not satisfied.")
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count == 0)
        {
            throw new ArgumentException(
                "A precondition exception requires at least one error.",
                nameof(errors));
        }

        _errors = Array.AsReadOnly(errors.ToArray());
    }

    public IReadOnlyList<SubmissionPreconditionError> Errors => _errors;
}
