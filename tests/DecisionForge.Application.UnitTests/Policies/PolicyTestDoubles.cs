using DecisionForge.Application.Policies.Ports;
using DecisionForge.Domain.Policies.Lifecycle;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.UnitTests.Policies;

internal sealed class RecordingPolicyRepository : IPolicyRepository
{
    public Policy? Existing { get; set; }

    public Policy? Added { get; private set; }

    public int FindCalls { get; private set; }

    public int SaveCalls { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task<Policy?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        FindCalls++;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(Existing?.Id == id ? Existing : null);
    }

    public Task AddAsync(Policy policy, CancellationToken cancellationToken)
    {
        Added = policy;
        LastCancellationToken = cancellationToken;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCalls++;
        LastCancellationToken = cancellationToken;
        return Task.CompletedTask;
    }
}

internal sealed class RecordingPolicyQueries : IPolicyQueries
{
    public bool CodeExists { get; set; }

    public int Calls { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public Task<bool> CodeExistsAsync(
        PolicyCode code,
        CancellationToken cancellationToken)
    {
        Calls++;
        LastCancellationToken = cancellationToken;
        return Task.FromResult(CodeExists);
    }
}

internal static class PolicyApplicationTestData
{
    public static readonly Guid PolicyId = Guid.Parse("91111111-1111-4111-8111-111111111111");
    public static readonly Guid VersionOneId = Guid.Parse("92222222-2222-4222-8222-222222222222");
    public static readonly Guid VersionTwoId = Guid.Parse("93333333-3333-4333-8333-333333333333");
    public static readonly Guid InitialTokenId = Guid.Parse("94444444-4444-4444-8444-444444444444");
    public static readonly Guid NextTokenId = Guid.Parse("95555555-5555-4555-8555-555555555555");
    public static readonly Guid ThirdTokenId = Guid.Parse("96666666-6666-4666-8666-666666666666");
    public static readonly DateTimeOffset Now = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

    public const string ValidJson =
        """
        {
          "schemaVersion":"1.0",
          "policyCode":"PROCUREMENT-GLOBAL",
          "name":"Global Procurement Policy",
          "defaultOutcome":{"disposition":"AutoApproved","reasonCode":"DEFAULT","message":"Default."},
          "rules":[]
        }
        """;

    public static Policy Existing(string json = ValidJson)
    {
        return Policy.Create(
            PolicyId,
            VersionOneId,
            PolicyCode.Parse("PROCUREMENT-GLOBAL"),
            "Global Procurement Policy",
            json,
            ConcurrencyToken.Create(InitialTokenId),
            Now);
    }
}
