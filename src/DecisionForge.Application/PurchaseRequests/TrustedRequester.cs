using DecisionForge.Application.Platform;
using DecisionForge.Domain.Common;

namespace DecisionForge.Application.PurchaseRequests;

internal static class TrustedRequester
{
    public static Guid RequiredUserId(ICurrentUserContext currentUser)
    {
        Guid? userId = currentUser.UserId;
        if (userId is null || userId == Guid.Empty)
        {
            throw new DomainRuleException(
                PurchaseRequestApplicationErrorCodes.Unauthenticated,
                "An authenticated user is required.");
        }

        return userId.Value;
    }
}
