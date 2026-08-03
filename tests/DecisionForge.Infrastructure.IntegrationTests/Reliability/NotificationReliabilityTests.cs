using System.Text.Json;
using DecisionForge.Application.Reliability.Notifications;
using DecisionForge.Domain.Notifications;
using DecisionForge.Infrastructure.Reliability;
using Microsoft.Extensions.Options;
using Npgsql;

namespace DecisionForge.Infrastructure.IntegrationTests.Reliability;

[Collection(ReliabilityTestGroup.Name)]
public sealed class NotificationReliabilityTests(ReliabilityContainerFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync()
    {
        return fixture.ResetAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task InAppNotificationIsIdempotentAndMailpitShowsLocalMessage()
    {
        Guid outboxId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        Notification notification = Notification.Create(
            Guid.Parse("22222222-2222-4222-8222-222222222222"),
            Guid.Parse("33333333-3333-4333-8333-333333333333"),
            outboxId,
            "requester@decisionforge.local",
            "Request approved",
            "Your request was approved.",
            "/requests/44444444-4444-4444-8444-444444444444",
            new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero));
        await using NpgsqlDataSource source = NpgsqlDataSource.Create(fixture.ConnectionString);
        PostgresReliabilityStore store = new(source);

        Assert.True(await store.CreateIfAbsentAsync(notification, CancellationToken.None));
        Assert.False(await store.CreateIfAbsentAsync(notification, CancellationToken.None));
        Assert.False(await store.HasEmailBeenDeliveredAsync(outboxId, CancellationToken.None));
        using HttpClient client = new() { BaseAddress = fixture.MailpitAddress };
        MailpitNotificationSender sender = new(
            client,
            Options.Create(new ReliabilityOptions
            {
                MailpitBaseAddress = fixture.MailpitAddress.ToString(),
                SenderEmail = "notifications@decisionforge.local",
            }));
        await sender.SendAsync(
            new NotificationDelivery(
                outboxId,
                notification.EmailAddress,
                notification.Subject,
                notification.Body),
            CancellationToken.None);
        Assert.True(await store.MarkEmailDeliveredAsync(
            outboxId,
            notification.CreatedAt.AddSeconds(1),
            CancellationToken.None));
        Assert.False(await store.MarkEmailDeliveredAsync(
            outboxId,
            notification.CreatedAt.AddSeconds(2),
            CancellationToken.None));
        Assert.True(await store.HasEmailBeenDeliveredAsync(outboxId, CancellationToken.None));

        using HttpResponseMessage response = await client.GetAsync(
            "api/v1/messages?start=0&limit=10",
            CancellationToken.None);
        response.EnsureSuccessStatusCode();
        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(CancellationToken.None));
        JsonElement message = Assert.Single(
            document.RootElement.GetProperty("messages").EnumerateArray().ToArray());
        Assert.Equal("Request approved", message.GetProperty("Subject").GetString());
    }
}
