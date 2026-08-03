using System.Security.Claims;
using DecisionForge.Api.Identity;
using DecisionForge.Application.Decisions;
using DecisionForge.Application.PurchaseRequests;
using Microsoft.AspNetCore.Http;

namespace DecisionForge.Api.IntegrationTests.Identity;

public sealed class TrustedCurrentUserContextTests
{
    [Fact]
    public void AuthenticatedNameIdentifierIsTheOnlyUserIdSource()
    {
        Guid expected = Guid.Parse("11111111-1111-4111-8111-111111111111");
        DefaultHttpContext context = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, expected.ToString("D"))],
                authenticationType: "Phase13Test")),
        };
        HttpCurrentUserContext currentUser = new(new HttpContextAccessor
        {
            HttpContext = context,
        });

        Assert.Equal(expected, currentUser.UserId);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", true)]
    [InlineData("not-a-guid", true)]
    [InlineData("00000000-0000-0000-0000-000000000000", true)]
    public void AnonymousOrInvalidIdentityNeverProducesTrustedUserId(
        string? value,
        bool authenticated)
    {
        Claim[] claims = value is null ? [] : [new Claim(ClaimTypes.NameIdentifier, value)];
        DefaultHttpContext context = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                claims,
                authenticated ? "Phase13Test" : null)),
        };
        HttpCurrentUserContext currentUser = new(new HttpContextAccessor
        {
            HttpContext = context,
        });

        Assert.Null(currentUser.UserId);
    }

    [Fact]
    public void RequestAndDecisionCommandsDoNotAcceptRequesterIdentity()
    {
        Type[] inputContracts =
        [
            typeof(CreatePurchaseRequestCommand),
            typeof(UpdatePurchaseRequestDraftCommand),
            typeof(AddPurchaseRequestItemCommand),
            typeof(UpdatePurchaseRequestItemCommand),
            typeof(RemovePurchaseRequestItemCommand),
            typeof(WithdrawPurchaseRequestCommand),
            typeof(ClonePurchaseRequestCommand),
            typeof(SubmitPurchaseRequestForDecisionCommand),
            typeof(RetryPurchaseRequestEvaluationCommand),
        ];

        Assert.All(inputContracts, contract =>
            Assert.DoesNotContain(
                contract.GetProperties(),
                property => property.Name.Contains("Requester", StringComparison.Ordinal)));
    }
}
