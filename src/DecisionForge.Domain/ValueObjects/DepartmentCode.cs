namespace DecisionForge.Domain.ValueObjects;

public sealed record DepartmentCode
{
    private DepartmentCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static DepartmentCode Parse(string value)
    {
        return new DepartmentCode(StringValueValidation.Code(value, 32, nameof(value)));
    }

    public override string ToString()
    {
        return Value;
    }
}
