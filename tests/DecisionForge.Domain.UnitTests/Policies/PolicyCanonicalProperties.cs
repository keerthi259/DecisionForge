using System.Globalization;
using DecisionForge.Domain.Policies.Contracts;
using DecisionForge.Domain.Policies.Parsing;
using DecisionForge.Domain.Policies.Serialization;
using FsCheck;
using FsCheck.Xunit;

namespace DecisionForge.Domain.UnitTests.Policies;

public sealed class PolicyCanonicalProperties
{
    [Property(MaxTest = 100)]
    public bool EquivalentDecimalSpellingsHaveIdenticalChecksum(PositiveInt input)
    {
        int amount = input.Get % 1_000_000 + 1;
        PolicyDefinition integer = Parse(NumericPolicy(amount.ToString(CultureInfo.InvariantCulture)));
        PolicyDefinition scaled = Parse(NumericPolicy($"{amount}.00"));

        return PolicyCanonicalSerializer.CalculateChecksum(integer)
            == PolicyCanonicalSerializer.CalculateChecksum(scaled);
    }

    [Property(MaxTest = 100)]
    public bool CanonicalRoundTripIsIdempotent(PositiveInt input)
    {
        int priority = input.Get % 10_000;
        string json = PolicyTestJson.Policy(PolicyTestJson.Rule(priority: priority));
        PolicyDefinition first = Parse(json);
        string canonical = PolicyCanonicalSerializer.Serialize(first);
        PolicyDefinition second = Parse(canonical);

        return canonical == PolicyCanonicalSerializer.Serialize(second);
    }

    private static string NumericPolicy(string value)
    {
        string condition = $$"""
        {"fact":"request.totalAmount","operator":"equals","value":{{value}}}
        """;
        return PolicyTestJson.Policy(PolicyTestJson.Rule(condition));
    }

    private static PolicyDefinition Parse(string json)
    {
        return PolicyJsonParser.Parse(json).Definition!;
    }
}
