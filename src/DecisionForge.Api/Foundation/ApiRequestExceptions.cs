using System.Collections.ObjectModel;

namespace DecisionForge.Api.Foundation;

public sealed class ApiRequestValidationException : Exception
{
    private readonly ReadOnlyCollection<ApiValidationError> _errors;

    public ApiRequestValidationException(IReadOnlyCollection<ApiValidationError> errors)
        : base("The request contains invalid fields.")
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count == 0)
        {
            throw new ArgumentException("At least one validation error is required.", nameof(errors));
        }

        _errors = Array.AsReadOnly(errors.ToArray());
    }

    public IReadOnlyList<ApiValidationError> Errors => _errors;
}

public sealed class ApiPreconditionException : Exception
{
    public ApiPreconditionException(int status, string code, string message)
        : base(message)
    {
        if (status is < 400 or > 499)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Status = status;
        Code = code;
    }

    public int Status { get; }

    public string Code { get; }
}

public sealed class RequestBodyTooLargeException : Exception
{
    public RequestBodyTooLargeException()
        : base("The request body exceeds the configured limit.")
    {
    }
}
