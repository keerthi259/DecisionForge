using DecisionForge.Domain.Policies.Evaluation;

namespace DecisionForge.Domain.UnitTests.Policies;

public sealed class PolicyTreeEvaluationTests
{
    [Fact]
    public void NestedAllAnyAndNotProduceCompleteDeterministicTrace()
    {
        string condition =
            """
            {
              "all": [
                {"fact":"supplier.isActive","operator":"equals","value":true},
                {
                  "any": [
                    {"fact":"request.totalAmount","operator":"lessThan","value":100},
                    {
                      "not": {
                        "fact":"request.currency",
                        "operator":"equals",
                        "value":"USD"
                      }
                    }
                  ]
                }
              ]
            }
            """;
        PolicyFactSet facts = PolicyFactSet.Create(
        [
            PolicyFact.Logical("supplier.isActive", true),
            PolicyFact.DecimalNumber("request.totalAmount", 500m),
            PolicyFact.Text("request.currency", "INR"),
        ]);

        PolicyEvaluationResult result = PolicyEvaluator.Evaluate(
            PolicyEvaluationTestData.Parse(
                PolicyTestJson.Policy(PolicyTestJson.Rule(condition))),
            facts);

        PolicyConditionEvaluation root = result.Rules[0].Condition;
        Assert.True(root.Result);
        Assert.Equal(PolicyConditionKind.All, root.Kind);
        Assert.Equal(2, root.Children.Count);
        Assert.Equal(PolicyConditionKind.Any, root.Children[1].Kind);
        Assert.Equal(2, root.Children[1].Children.Count);
        Assert.Equal(PolicyConditionKind.Not, root.Children[1].Children[1].Kind);
        Assert.False(root.Children[1].Children[0].Result);
        Assert.True(root.Children[1].Children[1].Result);
        Assert.Equal("request.totalAmount", root.Children[1].Children[0].FactAccesses[0].Path);
    }

    [Fact]
    public void LogicalNodesEvaluateEveryChildWithoutShortCircuitingTrace()
    {
        string condition =
            """
            {
              "any": [
                {"fact":"supplier.isActive","operator":"equals","value":true},
                {"fact":"request.currency","operator":"equals","value":"USD"}
              ]
            }
            """;
        PolicyFactSet facts = PolicyFactSet.Create(
        [
            PolicyFact.Logical("supplier.isActive", true),
            PolicyFact.Text("request.currency", "INR"),
        ]);

        PolicyEvaluationResult result = PolicyEvaluator.Evaluate(
            PolicyEvaluationTestData.Parse(
                PolicyTestJson.Policy(PolicyTestJson.Rule(condition))),
            facts);

        Assert.True(result.Rules[0].Condition.Result);
        Assert.Equal(2, result.Rules[0].Condition.Children.Count);
        Assert.False(result.Rules[0].Condition.Children[1].Result);
    }
}
