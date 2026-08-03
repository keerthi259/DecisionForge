using System.Security.Claims;
using DecisionForge.Api.Foundation;
using DecisionForge.Infrastructure.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;

namespace DecisionForge.Api.Identity;

public static class IdentityEndpointExtensions
{
    public static RouteGroupBuilder MapDecisionForgeIdentityEndpoints(
        this RouteGroupBuilder apiVersionOne)
    {
        ArgumentNullException.ThrowIfNull(apiVersionOne);
        RouteGroupBuilder group = apiVersionOne.MapGroup("/auth")
            .WithTags("Authentication");
        group.MapGet("/antiforgery", IssueAntiforgeryToken)
            .AllowAnonymous()
            .WithName("GetAntiforgeryToken")
            .WithSummary("Issue a cookie-bound browser antiforgery token.")
            .Produces<AntiforgeryTokenResponse>();
        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting(IdentityApiServiceCollectionExtensions.LoginRateLimitPolicyName)
            .AddEndpointFilter<AntiforgeryEndpointFilter>()
            .WithMetadata(new RequireAntiforgeryTokenAttribute())
            .WithName("Login")
            .WithSummary("Create a secure cookie session.")
            .Accepts<LoginRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ApiProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")
            .Produces<ApiProblemDetails>(StatusCodes.Status423Locked, "application/problem+json")
            .Produces<ApiProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json");
        group.MapPost("/logout", LogoutAsync)
            .RequireAuthorization()
            .AddEndpointFilter<AntiforgeryEndpointFilter>()
            .WithMetadata(new RequireAntiforgeryTokenAttribute())
            .WithName("Logout")
            .WithSummary("End the current cookie session.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ApiProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")
            .Produces<ApiProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json");
        group.MapGet("/me", GetCurrentUserAsync)
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .WithSummary("Return the authenticated user's roles and permissions.")
            .Produces<CurrentUserResponse>()
            .Produces<ApiProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json");
        return apiVersionOne;
    }

    private static IResult IssueAntiforgeryToken(HttpContext context, IAntiforgery antiforgery)
    {
        AntiforgeryTokenSet tokens = antiforgery.GetAndStoreTokens(context);
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
        string requestToken = tokens.RequestToken
            ?? throw new InvalidOperationException("authentication.antiforgery-token-unavailable");
        return Results.Ok(new AntiforgeryTokenResponse(
            requestToken,
            IdentityApiServiceCollectionExtensions.AntiforgeryHeaderName));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        SignInManager<DecisionForgeUser> signInManager,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!IsValidLogin(request))
        {
            return ApiProblemWriter.Result(
                context,
                StatusCodes.Status400BadRequest,
                "The login request is invalid.",
                ApiErrorCodes.ValidationField,
                errors: ValidateLogin(request));
        }

        SignInResult result = await signInManager.PasswordSignInAsync(
            request.Email!,
            request.Password!,
            isPersistent: false,
            lockoutOnFailure: true).WaitAsync(cancellationToken);
        if (result.Succeeded)
        {
            return Results.NoContent();
        }

        return result.IsLockedOut
            ? ApiProblemWriter.Result(
                context,
                StatusCodes.Status423Locked,
                "The account is temporarily locked.",
                "authentication.locked-out")
            : ApiProblemWriter.Result(
                context,
                StatusCodes.Status401Unauthorized,
                "The credentials are invalid.",
                "authentication.invalid-credentials");
    }

    private static async Task<IResult> LogoutAsync(
        SignInManager<DecisionForgeUser> signInManager,
        CancellationToken cancellationToken)
    {
        await signInManager.SignOutAsync().WaitAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        UserManager<DecisionForgeUser> userManager,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        string? id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        DecisionForgeUser? user = id is null
            ? null
            : await userManager.FindByIdAsync(id).WaitAsync(cancellationToken);
        if (user is null || user.Email is null)
        {
            return ApiProblemWriter.Result(
                context,
                StatusCodes.Status401Unauthorized,
                "Authentication is required.",
                "authentication.required");
        }

        IList<string> roles = await userManager.GetRolesAsync(user).WaitAsync(cancellationToken);
        IList<Claim> claims = await userManager.GetClaimsAsync(user).WaitAsync(cancellationToken);
        string[] permissions = claims
            .Where(claim => claim.Type == DecisionForgeIdentityPermissions.ClaimType)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return Results.Ok(new CurrentUserResponse(
            user.Id,
            user.Email,
            user.DisplayName,
            roles.Order(StringComparer.Ordinal).ToArray(),
            permissions));
    }

    private static bool IsValidLogin(LoginRequest request)
    {
        return request is
        {
            Email.Length: > 2 and <= 254,
            Password.Length: > 0 and <= 256,
        }
            && request.Email.Contains('@', StringComparison.Ordinal)
            && !request.Email.Any(char.IsWhiteSpace)
            && !request.Email.Any(char.IsControl);
    }

    private static List<ApiValidationError> ValidateLogin(LoginRequest request)
    {
        List<ApiValidationError> errors = [];
        if (request.Email is not { Length: > 2 and <= 254 }
            || !request.Email.Contains('@', StringComparison.Ordinal)
            || request.Email.Any(char.IsWhiteSpace)
            || request.Email.Any(char.IsControl))
        {
            errors.Add(new ApiValidationError(
                "login.email.invalid",
                "email",
                "A valid email address is required."));
        }

        if (request.Password is not { Length: > 0 and <= 256 })
        {
            errors.Add(new ApiValidationError(
                "login.password.invalid",
                "password",
                "A password between 1 and 256 characters is required."));
        }

        return errors;
    }
}
