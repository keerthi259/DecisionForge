using System.Security.Cryptography;
using System.Text;
using DecisionForge.Application.PurchaseRequests.Idempotency;

namespace DecisionForge.Application.Decisions;

internal static class SubmissionFingerprintBuilder
{
    public static SubmissionFingerprint Build(
        SubmitPurchaseRequestForDecisionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.ExpectedToken);
        string canonical = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"submit-decision\n{command.PurchaseRequestId:N}\n{command.ExpectedToken.Value:N}");
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return SubmissionFingerprint.Parse(Convert.ToHexStringLower(hash));
    }
}
