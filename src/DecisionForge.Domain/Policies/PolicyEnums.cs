namespace DecisionForge.Domain.Policies;

public enum PolicyOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    In,
    NotIn,
    Exists,
    NotExists,
    Contains,
}

public enum PolicyFactValueType
{
    DecimalNumber,
    Text,
    ControlledText,
    WholeNumber,
    Logical,
}

public enum PolicyApproverRole
{
    DepartmentApprover,
    ProcurementApprover,
    SecurityApprover,
    FinanceApprover,
    SeniorApprover,
}

public enum PolicyValidationSeverity
{
    Error,
    Warning,
}
