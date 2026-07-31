namespace DecisionForge.Domain.ValueObjects;

public sealed record SupplierRegistrationNumber
{
    private SupplierRegistrationNumber(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static SupplierRegistrationNumber Parse(string value)
    {
        return new SupplierRegistrationNumber(
            StringValueValidation.Code(value, 64, nameof(value), "-_/"));
    }

    public override string ToString()
    {
        return Value;
    }
}
