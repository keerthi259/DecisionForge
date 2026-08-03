namespace DecisionForge.Api.Identity;

public sealed record LoginRequest(string? Email, string? Password);

public sealed record AntiforgeryTokenResponse(string RequestToken, string HeaderName);

public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
