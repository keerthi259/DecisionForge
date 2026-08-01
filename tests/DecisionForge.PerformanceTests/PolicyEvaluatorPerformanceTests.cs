using System.Diagnostics;
using DecisionForge.Domain.Policies.Contracts;
using DecisionForge.Domain.Policies.Evaluation;
using Xunit.Abstractions;

namespace DecisionForge.PerformanceTests;

public sealed class PolicyEvaluatorPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public PolicyEvaluatorPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void HundredRuleEvaluatorP95IsBelowFiftyMilliseconds()
    {
        PolicyDefinition policy = PolicyPerformanceScenario.CreatePolicy();
        PolicyFactSet facts = PolicyPerformanceScenario.CreateFacts();
        for (int iteration = 0; iteration < 50; iteration++)
        {
            _ = PolicyEvaluator.Evaluate(policy, facts);
        }

        double[] elapsedMilliseconds = new double[500];
        for (int iteration = 0; iteration < elapsedMilliseconds.Length; iteration++)
        {
            long started = Stopwatch.GetTimestamp();
            _ = PolicyEvaluator.Evaluate(policy, facts);
            elapsedMilliseconds[iteration] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }

        Array.Sort(elapsedMilliseconds);
        int p95Index = (int)Math.Ceiling(elapsedMilliseconds.Length * 0.95) - 1;
        double p95 = elapsedMilliseconds[p95Index];

        _output.WriteLine($"100-rule evaluator p95: {p95:F3} ms");
        Assert.True(p95 < 50, $"100-rule evaluator p95 was {p95:F3} ms; budget is < 50 ms.");
    }
}
