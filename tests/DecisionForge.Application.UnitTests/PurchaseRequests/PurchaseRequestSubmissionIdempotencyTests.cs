using DecisionForge.Application.PurchaseRequests;
using DecisionForge.Application.PurchaseRequests.Idempotency;
using DecisionForge.Domain.Common;
using DecisionForge.Domain.ValueObjects;

namespace DecisionForge.Application.UnitTests.PurchaseRequests;

public sealed class PurchaseRequestSubmissionIdempotencyTests
{
    private const string _fingerprintA =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string _fingerprintB =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Theory]
    [InlineData("")]
    [InlineData("ABCDEF")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public void FingerprintRejectsMalformedOrNonCanonicalInput(string value)
    {
        DomainRuleException exception = Assert.Throws<DomainRuleException>(
            () => SubmissionFingerprint.Parse(value));

        Assert.Equal(DomainErrorCodes.Validation, exception.Code);
    }

    [Fact]
    public void NewKeyRequiresExecution()
    {
        SubmissionIdempotencyResolution resolution =
            PurchaseRequestSubmissionIdempotency.Resolve(
                null,
                SubmissionFingerprint.Parse(_fingerprintA));

        Assert.False(resolution.IsReplay);
        Assert.Null(resolution.OriginalPurchaseRequestId);
    }

    [Fact]
    public void MatchingFingerprintReplaysOriginalRequestReference()
    {
        PurchaseRequestSubmissionRecord existing = Record(_fingerprintA);

        SubmissionIdempotencyResolution resolution =
            PurchaseRequestSubmissionIdempotency.Resolve(
                existing,
                SubmissionFingerprint.Parse(_fingerprintA));

        Assert.True(resolution.IsReplay);
        Assert.Equal(PurchaseRequestApplicationTestData.RequestId, resolution.OriginalPurchaseRequestId);
    }

    [Fact]
    public void DifferentFingerprintReturnsStableConflict()
    {
        PurchaseRequestSubmissionRecord existing = Record(_fingerprintA);

        DomainRuleException exception = Assert.Throws<DomainRuleException>(
            () => PurchaseRequestSubmissionIdempotency.Resolve(
                existing,
                SubmissionFingerprint.Parse(_fingerprintB)));

        Assert.Equal(PurchaseRequestApplicationErrorCodes.IdempotencyConflict, exception.Code);
        Assert.DoesNotContain(_fingerprintA, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(_fingerprintB, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StoredRecordRequiresValidIdentityAndUtcCompletionTime()
    {
        IdempotencyKey key = IdempotencyKey.Parse("submit-key-1");
        SubmissionFingerprint fingerprint = SubmissionFingerprint.Parse(_fingerprintA);

        Assert.Throws<DomainRuleException>(
            () => PurchaseRequestSubmissionRecord.Create(
                Guid.Empty,
                key,
                fingerprint,
                PurchaseRequestApplicationTestData.RequestId,
                PurchaseRequestApplicationTestData.CurrentTime));
        Assert.Throws<DomainRuleException>(
            () => PurchaseRequestSubmissionRecord.Create(
                PurchaseRequestApplicationTestData.RequesterId,
                key,
                fingerprint,
                PurchaseRequestApplicationTestData.RequestId,
                PurchaseRequestApplicationTestData.CurrentTime.ToOffset(TimeSpan.FromHours(1))));
    }

    private static PurchaseRequestSubmissionRecord Record(string fingerprint)
    {
        return PurchaseRequestSubmissionRecord.Create(
            PurchaseRequestApplicationTestData.RequesterId,
            IdempotencyKey.Parse("submit-key-1"),
            SubmissionFingerprint.Parse(fingerprint),
            PurchaseRequestApplicationTestData.RequestId,
            PurchaseRequestApplicationTestData.CurrentTime);
    }
}
