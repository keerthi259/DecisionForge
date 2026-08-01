using BenchmarkDotNet.Attributes;
using DecisionForge.Domain.Policies.Contracts;
using DecisionForge.Domain.Policies.Evaluation;
using DecisionForge.Domain.Policies.Parsing;
using DecisionForge.Domain.Policies.Validation;

namespace DecisionForge.PerformanceTests;

[MemoryDiagnoser]
public class PolicyEvaluatorBenchmark
{
    private PolicyDefinition _policy = null!;
    private PolicyFactSet _facts = null!;

    [GlobalSetup]
    public void Setup()
    {
        _policy = PolicyPerformanceScenario.CreatePolicy();
        _facts = PolicyPerformanceScenario.CreateFacts();
    }

    [Benchmark]
    public PolicyEvaluationResult EvaluateHundredRules()
    {
        return PolicyEvaluator.Evaluate(_policy, _facts);
    }
}

internal static class PolicyPerformanceScenario
{
    public static PolicyDefinition CreatePolicy()
    {
        string rules = string.Join(
            ',',
            Enumerable.Range(1, 100).Select(index => $$"""
            {
              "id": "RULE-{{index:D3}}",
              "priority": {{101 - index}},
              "when": {
                "fact": "request.totalAmount",
                "operator": "greaterThan",
                "value": {{index}}
              },
              "then": {
                "disposition": "AutoApproved",
                "reasonCode": "RULE_MATCHED",
                "message": "The performance rule matched."
              }
            }
            """));
        string json = $$"""
        {
          "schemaVersion": "1.0",
          "policyCode": "PERFORMANCE-POLICY",
          "name": "Performance policy",
          "defaultOutcome": {
            "disposition": "AutoApproved",
            "reasonCode": "DEFAULT_OUTCOME",
            "message": "The default outcome applies."
          },
          "rules": [{{rules}}]
        }
        """;
        PolicyParseResult parsed = PolicyJsonParser.Parse(json);
        return parsed.Definition
            ?? throw new InvalidOperationException("The benchmark policy must be valid.");
    }

    public static PolicyFactSet CreateFacts()
    {
        return PolicyFactSet.Create(
            [PolicyFact.DecimalNumber("request.totalAmount", 1_000m)]);
    }
}
