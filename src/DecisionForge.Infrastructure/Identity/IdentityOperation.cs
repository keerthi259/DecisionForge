using Microsoft.AspNetCore.Identity;

namespace DecisionForge.Infrastructure.Identity;

internal static class IdentityOperation
{
    public static void EnsureSucceeded(IdentityResult result, string errorCode)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Succeeded)
        {
            return;
        }

        string codes = string.Join(
            ',',
            result.Errors.Select(error => error.Code).Order(StringComparer.Ordinal));
        throw new InvalidOperationException($"{errorCode}:{codes}");
    }
}
