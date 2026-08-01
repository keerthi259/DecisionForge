using DecisionForge.Domain.Enums;
using DecisionForge.Domain.EvaluationFacts;
using DecisionForge.Domain.Policies;
using DecisionForge.Domain.Policies.Contracts;
using DecisionForge.Domain.Policies.Evaluation;
using DecisionForge.Domain.PurchaseRequests;
using DecisionForge.Domain.UnitTests.Builders;

namespace DecisionForge.Domain.UnitTests.Policies;

public sealed class PolicyFactSetTests
{
    [Fact]
    public void SnapshotMapsExactlyEveryApprovedTypedFact()
    {
        PurchaseRequest request = new PurchaseRequestBuilder()
            .WithItem(quantity: 30, unitPrice: 80_000m, category: ProcurementCategory.Hardware)
            .Build();
        EvaluationFactSnapshot snapshot = EvaluationFactSnapshot.Create(
            request,
            new DepartmentBuilder().Build(),
            new SupplierBuilder().Build(),
            new DateOnly(2026, 8, 1));

        PolicyFactSet facts = PolicyFactSet.FromSnapshot(snapshot);

        Assert.Equal(16, facts.Facts.Count);
        Assert.Equal(
            facts.Facts.OrderBy(fact => fact.Path, StringComparer.Ordinal),
            facts.Facts);
        Assert.Contains(
            facts.Facts,
            fact => fact.Path == "request.totalAmount"
                && fact.ValueType == PolicyFactValueType.DecimalNumber);
        Assert.Contains(
            facts.Facts,
            fact => fact.Path == "request.itemCount"
                && fact.ValueType == PolicyFactValueType.WholeNumber);
        Assert.Contains(
            facts.Facts,
            fact => fact.Path == "request.category"
                && fact.ValueType == PolicyFactValueType.ControlledText);
    }

    [Fact]
    public void FactSetNormalizesInputOrderForStableChecksum()
    {
        PolicyFact first = PolicyFact.Text("request.currency", "INR");
        PolicyFact second = PolicyFact.DecimalNumber("request.totalAmount", 500_000.00m);
        PolicyDefinition policy = PolicyEvaluationTestData.SingleRule(
            "request.totalAmount",
            "equals",
            "500000");

        PolicyEvaluationResult forward = PolicyEvaluator.Evaluate(
            policy,
            PolicyFactSet.Create([first, second]));
        PolicyEvaluationResult reverse = PolicyEvaluator.Evaluate(
            policy,
            PolicyFactSet.Create([second, first]));

        Assert.Equal(forward.InputChecksum, reverse.InputChecksum);
        Assert.Equal(forward.TraceChecksum, reverse.TraceChecksum);
    }

    [Fact]
    public void MissingRequiredFactFailsWithControlledError()
    {
        PolicyDefinition policy = PolicyEvaluationTestData.SingleRule(
            "request.totalAmount",
            "greaterThan",
            "1");

        PolicyEvaluationException exception = Assert.Throws<PolicyEvaluationException>(
            () => PolicyEvaluator.Evaluate(policy, PolicyFactSet.Create([])));

        Assert.Equal(PolicyEvaluationErrorCodes.MissingFact, exception.Code);
        Assert.Equal("request.totalAmount", exception.Path);
        Assert.Equal("A fact required by policy evaluation is missing.", exception.Message);
    }

    [Fact]
    public void ExistenceOperatorsTraceAbsentFactsWithoutFailure()
    {
        PolicyDefinition exists = PolicyEvaluationTestData.SingleRule(
            "request.currency",
            "exists",
            null);
        PolicyDefinition notExists = PolicyEvaluationTestData.SingleRule(
            "request.currency",
            "notExists",
            null);
        PolicyFactSet empty = PolicyFactSet.Create([]);

        PolicyEvaluationResult existsResult = PolicyEvaluator.Evaluate(exists, empty);
        PolicyEvaluationResult notExistsResult = PolicyEvaluator.Evaluate(notExists, empty);

        Assert.False(existsResult.Rules[0].Matched);
        Assert.True(notExistsResult.Rules[0].Matched);
        PolicyFactAccess access = notExistsResult.Rules[0].Condition.FactAccesses[0];
        Assert.False(access.Exists);
        Assert.Null(access.Value);
    }

    [Fact]
    public void UnknownDuplicateAndIncorrectlyTypedFactsFailSafely()
    {
        PolicyEvaluationException unknown = Assert.Throws<PolicyEvaluationException>(
            () => PolicyFact.Text("request.secret", "value"));
        PolicyEvaluationException wrongType = Assert.Throws<PolicyEvaluationException>(
            () => PolicyFact.Text("request.totalAmount", "10"));
        PolicyEvaluationException invalidEnum = Assert.Throws<PolicyEvaluationException>(
            () => PolicyFact.ControlledText("request.urgency", "urgent"));
        PolicyFact duplicate = PolicyFact.Text("request.currency", "INR");
        PolicyEvaluationException duplicateError = Assert.Throws<PolicyEvaluationException>(
            () => PolicyFactSet.Create([duplicate, duplicate]));

        Assert.Equal(PolicyEvaluationErrorCodes.UnknownFact, unknown.Code);
        Assert.Equal(PolicyEvaluationErrorCodes.FactTypeMismatch, wrongType.Code);
        Assert.Equal(PolicyEvaluationErrorCodes.FactTypeMismatch, invalidEnum.Code);
        Assert.Equal(PolicyEvaluationErrorCodes.DuplicateFact, duplicateError.Code);
    }

    [Fact]
    public void FactFactoriesRejectNullInputs()
    {
        Assert.Throws<ArgumentNullException>(() => PolicyFact.Text(null!, "INR"));
        Assert.Throws<ArgumentNullException>(() => PolicyFact.Text("request.currency", null!));
        Assert.Throws<ArgumentNullException>(
            () => PolicyFact.ControlledText("request.urgency", null!));
        Assert.Throws<ArgumentNullException>(() => PolicyFactSet.Create(null!));
        Assert.Throws<ArgumentNullException>(
            () => PolicyFactSet.FromSnapshot(null!));
    }
}
