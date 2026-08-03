using System.Net.Http.Json;
using DecisionForge.Application.Reliability.Notifications;
using DecisionForge.Application.Reliability.Outbox;
using Microsoft.Extensions.Options;

namespace DecisionForge.Infrastructure.Reliability;

public sealed class MailpitNotificationSender(
    HttpClient httpClient,
    IOptions<ReliabilityOptions> options) : INotificationSender
{
    public async Task SendAsync(
        NotificationDelivery delivery,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        MailpitSendRequest request = new(
            new MailpitAddress(options.Value.SenderEmail, "DecisionForge"),
            [new MailpitAddress(delivery.RecipientEmail, null)],
            delivery.Subject,
            delivery.Body,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Message-ID"] = $"<{delivery.DeliveryId:D}@decisionforge.local>",
                ["X-DecisionForge-Delivery-ID"] = delivery.DeliveryId.ToString("D"),
            });
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            "api/v1/send",
            request,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new OutboxDeliveryException(
                "notification-transport-failed",
                "The notification transport rejected the message.");
        }
    }

    private sealed record MailpitAddress(string Email, string? Name);

    private sealed record MailpitSendRequest(
        MailpitAddress From,
        IReadOnlyList<MailpitAddress> To,
        string Subject,
        string Text,
        IReadOnlyDictionary<string, string> Headers);
}
