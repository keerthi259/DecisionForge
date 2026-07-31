using DecisionForge.Domain.ReferenceData;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.UnitTests.Builders;

internal sealed class DepartmentBuilder
{
    public static readonly Guid DefaultId = PurchaseRequestBuilder.DefaultDepartmentId;
    public static readonly ConcurrencyToken DefaultToken = ConcurrencyToken.Create(
        Guid.Parse("55555555-5555-7555-8555-555555555555"));
    public static readonly ConcurrencyToken NextToken = ConcurrencyToken.Create(
        Guid.Parse("55555555-5555-7555-8555-555555555556"));

    private Guid _id = DefaultId;
    private string _name = "Engineering";
    private Money _autoApprovalLimit = Money.Create(250_000m, CurrencyCode.Parse("INR"));

    public DepartmentBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public DepartmentBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public DepartmentBuilder WithAutoApprovalLimit(Money autoApprovalLimit)
    {
        _autoApprovalLimit = autoApprovalLimit;
        return this;
    }

    public Department Build()
    {
        return Department.Create(
            _id,
            DepartmentCode.Parse("ENG"),
            _name,
            _autoApprovalLimit,
            DefaultToken,
            PurchaseRequestBuilder.DefaultTime);
    }
}
