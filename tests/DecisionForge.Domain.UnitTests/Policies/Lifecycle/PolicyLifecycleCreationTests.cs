using DecisionForge.Domain.Common;
using DecisionForge.Domain.Policies.Lifecycle;
using DecisionForge.Domain.Policies.Lifecycle.Events;
using DecisionForge.Domain.Policies.Parsing;
using DecisionForge.Domain.Policies.Serialization;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Domain.UnitTests.Policies.Lifecycle;

public sealed class PolicyLifecycleCreationTests
{
    [Fact]
    public void CreateBuildsVersionOneAndRaisesSafeEvents()
    {
        Policy policy = PolicyLifecycleTestData.Create();
        PolicyVersion version = Assert.Single(policy.Versions);

        Assert.Equal(PolicyLifecycleTestData.PolicyId, policy.Id);
        Assert.Equal("PROCUREMENT-GLOBAL", policy.Code.Value);
        Assert.Equal("Global Procurement Policy", policy.Name);
        Assert.Equal(1, version.Number.Value);
        Assert.True(version.IsValid);
        Assert.NotNull(version.Checksum);
        Assert.Equal(PolicyLifecycleTestData.CreatedAt, version.CreatedAt);
        Assert.Collection(
            policy.DomainEvents,
            domainEvent =>
            {
                PolicyCreatedDomainEvent created =
                    Assert.IsType<PolicyCreatedDomainEvent>(domainEvent);
                Assert.Equal(policy.Id, created.PolicyId);
                Assert.Equal(policy.Code, created.Code);
            },
            domainEvent =>
            {
                PolicyVersionDraftCreatedDomainEvent created =
                    Assert.IsType<PolicyVersionDraftCreatedDomainEvent>(domainEvent);
                Assert.Equal(version.Id, created.PolicyVersionId);
                Assert.True(created.IsValid);
                Assert.Equal(version.Checksum, created.Checksum);
                Assert.DoesNotContain("rules", created.ToString(), StringComparison.OrdinalIgnoreCase);
            });
    }

    [Fact]
    public void InvalidDraftIsPreservedWithoutDefinitionOrChecksum()
    {
        const string malformed = "{ not-json";

        Policy policy = PolicyLifecycleTestData.Create(malformed);
        PolicyVersion version = Assert.Single(policy.Versions);

        Assert.Equal(malformed, version.DefinitionJson);
        Assert.False(version.IsValid);
        Assert.Null(version.Definition);
        Assert.Null(version.Checksum);
        Assert.Equal("policy.json.malformed", Assert.Single(version.ValidationErrors).Code);
        DomainRuleException exception = Assert.Throws<DomainRuleException>(() => policy.Publish(
            version.Id,
            PolicyLifecycleTestData.CreatedAt.AddHours(1),
            null,
            PolicyLifecycleTestData.InitialToken,
            PolicyLifecycleTestData.SecondToken,
            PolicyLifecycleTestData.CreatedAt));
        Assert.Equal(PolicyLifecycleErrorCodes.InvalidDefinition, exception.Code);
        Assert.Equal(PolicyLifecycleTestData.InitialToken, policy.ConcurrencyToken);
    }

    [Theory]
    [InlineData("OTHER-POLICY", "Global Procurement Policy", "policy.identity.code-mismatch")]
    [InlineData("PROCUREMENT-GLOBAL", "Another policy", "policy.identity.name-mismatch")]
    public void DraftDefinitionMustMatchPolicyIdentity(
        string code,
        string name,
        string expectedError)
    {
        Policy policy = PolicyLifecycleTestData.Create(
            PolicyLifecycleTestData.Definition(code: code, name: name));

        PolicyVersion version = Assert.Single(policy.Versions);
        Assert.False(version.IsValid);
        Assert.Equal(expectedError, Assert.Single(version.ValidationErrors).Code);
    }

    [Fact]
    public void EquivalentValidDefinitionsProduceSameCanonicalChecksum()
    {
        string original = PolicyLifecycleTestData.Definition();
        Policy policy = PolicyLifecycleTestData.Create(original);
        PolicyVersion version = Assert.Single(policy.Versions);
        string reordered =
            """
            {"rules":[{"then":{"message":"Rule A matched.","reasonCode":"RULE_A","disposition":"Rejected"},"when":{"value":true,"operator":"equals","fact":"supplier.isActive"},"priority":10,"id":"RULE-A"}],"name":"Global Procurement Policy","defaultOutcome":{"message":"The default outcome applies.","reasonCode":"DEFAULT_OUTCOME","disposition":"AutoApproved"},"policyCode":"PROCUREMENT-GLOBAL","schemaVersion":"1.0"}
            """;

        policy.UpdateDraft(
            version.Id,
            reordered,
            PolicyLifecycleTestData.InitialToken,
            PolicyLifecycleTestData.SecondToken,
            PolicyLifecycleTestData.CreatedAt.AddMinutes(1));

        Assert.Equal(
            PolicyCanonicalSerializer.CalculateChecksum(
                PolicyJsonParser.Parse(original).Definition!),
            version.Checksum);
        Assert.Equal(reordered, version.DefinitionJson);
    }

    [Fact]
    public void PublicVersionCollectionCannotBeMutated()
    {
        Policy policy = PolicyLifecycleTestData.Create();
        ICollection<PolicyVersion> versions =
            Assert.IsAssignableFrom<ICollection<PolicyVersion>>(policy.Versions);

        Assert.Throws<NotSupportedException>(versions.Clear);
    }

    [Fact]
    public void PolicyCodeValidatesAndNormalizesInput()
    {
        Assert.Equal("PROCUREMENT-GLOBAL", PolicyCode.Parse(" procurement-global ").Value);
        AssertValidation(() => PolicyCode.Parse(null));
        AssertValidation(() => PolicyCode.Parse("code with spaces"));
        AssertValidation(() => PolicyCode.Parse(new string('A', 65)));
    }

    private static void AssertValidation(Action action)
    {
        DomainRuleException exception = Assert.Throws<DomainRuleException>(action);
        Assert.Equal(DomainErrorCodes.Validation, exception.Code);
    }
}
