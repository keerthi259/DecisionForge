using System.Security.Claims;
using DecisionForge.Application.Platform;

namespace DecisionForge.Api.Identity;

public sealed class HttpCurrentUserContext(IHttpContextAccessor httpContextAccessor)
    : ICurrentUserContext
{
    public Guid? UserId
    {
        get
        {
            ClaimsPrincipal? principal = httpContextAccessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            string? value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out Guid userId) && userId != Guid.Empty
                ? userId
                : null;
        }
    }
}
