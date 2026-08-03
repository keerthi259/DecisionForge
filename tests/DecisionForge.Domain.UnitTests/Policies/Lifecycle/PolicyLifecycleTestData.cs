using System.Text.Json;
using DecisionForge.Domain.Policies.Lifecycle;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.UnitTests.Policies.Lifecycle;

internal static class PolicyLifecycleTestData
{
    public static readonly Guid PolicyId = Guid.Parse("81111111-1111-4111-8111-111111111111");
    public static readonly Guid VersionOneId = Guid.Parse("82222222-2222-4222-8222-222222222222");
    public static readonly Guid VersionTwoId = Guid.Parse("83333333-3333-4333-8333-333333333333");
    public static readonly ConcurrencyToken InitialToken = ConcurrencyToken.Create(
        Guid.Parse("84444444-4444-4444-8444-444444444444"));
    public static readonly ConcurrencyToken SecondToken = ConcurrencyToken.Create(
        Guid.Parse("85555555-5555-4555-8555-555555555555"));
    public static readonly ConcurrencyToken ThirdToken = ConcurrencyToken.Create(
        Guid.Parse("86666666-6666-4666-8666-666666666666"));
    public static readonly ConcurrencyToken FourthToken = ConcurrencyToken.Create(
        Guid.Parse("87777777-7777-4777-8777-777777777777"));
    public static readonly DateTimeOffset CreatedAt = new(
        2026,
        8,
        1,
        12,
        0,
        0,
        TimeSpan.Zero);

    public static Policy Create(string? json = null)
    {
        return Policy.Create(
            PolicyId,
            VersionOneId,
            PolicyCode.Parse("PROCUREMENT-GLOBAL"),
            "Global Procurement Policy",
            json ?? Definition(),
            InitialToken,
            CreatedAt);
    }

    public static string Definition(
        string? rules = null,
        string? defaultOutcome = null,
        string code = "PROCUREMENT-GLOBAL",
        string name = "Global Procurement Policy")
    {
        return PolicyTestJson.Policy(
            rules ?? Rule(),
            defaultOutcome,
            policyCode: code,
            name: name);
    }

    public static string Rule(
        string id = "RULE-A",
        int priority = 10,
        string? fact = null,
        string @operator = "equals",
        string value = "true",
        string disposition = "Rejected",
        string reasonCode = "RULE_A",
        string message = "Rule A matched.")
    {
        string condition = $$"""
        {"fact":{{JsonSerializer.Serialize(fact ?? "supplier.isActive")}},"operator":{{JsonSerializer.Serialize(@operator)}},"value":{{value}}}
        """;
        return PolicyTestJson.Rule(
            condition,
            PolicyTestJson.Outcome(
                disposition,
                null,
                reasonCode,
                message),
            id,
            priority);
    }
}
