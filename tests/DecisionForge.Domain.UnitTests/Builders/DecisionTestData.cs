using DecisionForge.Domain.Enums;
using DecisionForge.Domain.EvaluationFacts;
using DecisionForge.Domain.Policies.Contracts;
using DecisionForge.Domain.Policies.Parsing;
using DecisionForge.Domain.Policies.Selection;
using DecisionForge.Domain.Policies.Serialization;
using DecisionForge.Domain.Policies.Validation;
using DecisionForge.Domain.PurchaseRequests;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.UnitTests.Builders;

internal static class DecisionTestData
{
    public static readonly Guid PolicyId =
        Guid.Parse("77777777-7777-4777-8777-777777777777");
    public static readonly Guid PolicyVersionId =
        Guid.Parse("88888888-8888-4888-8888-888888888888");
    public static readonly DateTimeOffset EffectiveFrom =
        PurchaseRequestBuilder.DefaultTime.AddDays(-1);

    public static PolicyEvaluationSource PolicySource(
        PolicyStatus status = PolicyStatus.Published,
        DateTimeOffset? effectiveFrom = null,
        DateTimeOffset? effectiveUntil = null,
        PolicyDefinition? definition = null,
        Guid? policyId = null,
        Guid? versionId = null)
    {
        PolicyDefinition selectedDefinition = definition ?? Definition();
        return PolicyEvaluationSource.Create(
            policyId ?? PolicyId,
            versionId ?? PolicyVersionId,
            PolicyVersionNumber.Create(1),
            status,
            PolicyCanonicalSerializer.CalculateChecksum(selectedDefinition),
            selectedDefinition,
            effectiveFrom ?? EffectiveFrom,
            effectiveUntil);
    }

    public static PurchaseRequestEvaluationContext Context(
        PurchaseRequest purchaseRequest,
        PolicyEvaluationSource? policy = null)
    {
        EvaluationFactSnapshot snapshot = EvaluationFactSnapshot.Create(
            purchaseRequest,
            new DepartmentBuilder().Build(),
            new SupplierBuilder().Build(),
            DateOnly.FromDateTime(PurchaseRequestBuilder.DefaultTime.UtcDateTime));
        return PurchaseRequestEvaluationContext.Create(policy ?? PolicySource(), snapshot);
    }

    public static PolicyDefinition Definition()
    {
        const string json =
            """
            {
              "schemaVersion":"1.0",
              "policyCode":"TEST-POLICY",
              "name":"Test policy",
              "defaultOutcome":{
                "disposition":"AutoApproved",
                "reasonCode":"DEFAULT",
                "message":"Default outcome."
              },
              "rules":[{
                "id":"ACTIVE-SUPPLIER",
                "priority":1,
                "when":{"fact":"supplier.isActive","operator":"equals","value":false},
                "then":{
                  "disposition":"Rejected",
                  "reasonCode":"SUPPLIER_INACTIVE",
                  "message":"Supplier is inactive."
                }
              }]
            }
            """;
        PolicyParseResult parsed = PolicyJsonParser.Parse(json);
        return parsed.Definition!;
    }
}
