using DecisionForge.Application.Decisions;
using DecisionForge.Application.PurchaseRequests.Submission;
using DecisionForge.Application.UnitTests.PurchaseRequests;
using DecisionForge.Domain.Policies.Evaluation;
using DecisionForge.Domain.PurchaseRequests;

namespace DecisionForge.Application.UnitTests.Decisions;

public sealed class NormalizedEvaluationInputBuilderTests
{
    [Fact]
    public void BuilderCopiesOnlyTheSixteenApprovedPolicyFacts()
    {
        PurchaseRequest request = PurchaseRequestApplicationTestData.CreateRequest(withItem: true);
        SubmissionPreconditionResult validation = new(
            [],
            DecisionApplicationTestData.Department(),
            DecisionApplicationTestData.Supplier());

        PolicyFactSet facts = PolicyFactSet.FromSnapshot(
            NormalizedEvaluationInputBuilder.Build(
                request,
                validation,
                new DateOnly(2026, 8, 3)));

        Assert.Equal(16, facts.Facts.Count);
        Assert.Equal(
            [
                "department.autoApprovalLimit",
                "department.code",
                "derived.containsTechnologyPurchase",
                "derived.requiresUrgencyException",
                "request.category",
                "request.currency",
                "request.dataSensitivity",
                "request.expectedDeliveryDays",
                "request.hasBusinessJustification",
                "request.itemCount",
                "request.totalAmount",
                "request.urgency",
                "supplier.isActive",
                "supplier.isApproved",
                "supplier.onboardingStatus",
                "supplier.riskRating",
            ],
            facts.Facts.Select(fact => fact.Path));
        Assert.DoesNotContain(facts.Facts, fact => fact.Path.Contains("name", StringComparison.Ordinal));
        Assert.DoesNotContain(
            facts.Facts,
            fact => fact.Path.Contains("registration", StringComparison.Ordinal));
    }

    [Fact]
    public void InvalidPreconditionsArePreservedAsStructuredErrors()
    {
        SubmissionPreconditionError error = new(
            "purchase-request.supplier-inactive",
            "metadata.supplierId",
            "The selected supplier is inactive.");

        SubmissionPreconditionException exception = Assert.Throws<SubmissionPreconditionException>(
            () => NormalizedEvaluationInputBuilder.Build(
                PurchaseRequestApplicationTestData.CreateRequest(withItem: true),
                new SubmissionPreconditionResult([error]),
                new DateOnly(2026, 8, 3)));

        Assert.Same(error, Assert.Single(exception.Errors));
    }

    [Fact]
    public void ValidFlagWithoutReferenceSnapshotsStillFailsWithControlledErrors()
    {
        SubmissionPreconditionException exception = Assert.Throws<SubmissionPreconditionException>(
            () => NormalizedEvaluationInputBuilder.Build(
                PurchaseRequestApplicationTestData.CreateRequest(withItem: true),
                new SubmissionPreconditionResult([]),
                new DateOnly(2026, 8, 3)));

        Assert.Equal(
            [
                "purchase-request.department-not-found",
                "purchase-request.supplier-not-found",
            ],
            exception.Errors.Select(error => error.Code));
    }
}
