using LoyaltyCloud.Application.Billing;
using LoyaltyCloud.Application.GiftCards;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LoyaltyCloud.Tests.Infrastructure;

public sealed class GiftCardDeliveryTests
{
    [Fact]
    public async Task DisabledEmail_DoesNotCallSender()
    {
        var sender = new RecordingSender();
        var service = new GiftCardDeliveryService(sender, new Configuration(false), NullLogger<GiftCardDeliveryService>.Instance);

        var result = await service.SendEmailAsync(Card(), "recipient@example.test", "KBeauty");

        Assert.Equal(GiftCardDeliveryStatus.NotSent, result.Status);
        Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task EnabledEmail_ContainsSenderMessageAndCorrectClaimUrl_WithHtmlEncoding()
    {
        var sender = new RecordingSender();
        var service = new GiftCardDeliveryService(sender, new Configuration(true), NullLogger<GiftCardDeliveryService>.Instance);

        var canonicalUrl = await service.GetClaimUrlAsync("claim-token-123");
        var result = await service.SendEmailAsync(Card("Daniel <script>", "Disfruta & celebra"), "recipient@example.test", "KBeauty <store>");

        var email = Assert.Single(sender.Messages);
        Assert.Equal(GiftCardDeliveryStatus.Sent, result.Status);
        Assert.Equal("KBeauty <store> te envió una Gift Card", email.Subject);
        Assert.Equal("recipient@example.test", email.Recipient);
        Assert.Equal("https://admin.example.test/giftcards/claim/claim-token-123", canonicalUrl);
        Assert.Contains(canonicalUrl!, email.TextBody);
        Assert.Contains("KBeauty &lt;store&gt;", email.HtmlBody);
        Assert.Contains("500.00 MXN", email.TextBody);
        Assert.Contains("Ver mi Gift Card", email.HtmlBody);
        Assert.Contains("Daniel &lt;script&gt;", email.HtmlBody);
        Assert.Contains("Disfruta &amp; celebra", email.HtmlBody);
        Assert.DoesNotContain("<script>", email.HtmlBody);
    }

    [Fact]
    public async Task MissingRecipient_ReturnsNotSentWithoutCallingSender()
    {
        var sender = new RecordingSender();
        var service = new GiftCardDeliveryService(sender, new Configuration(true), NullLogger<GiftCardDeliveryService>.Instance);

        var result = await service.SendEmailAsync(Card(), "", "KBeauty");

        Assert.Equal(GiftCardDeliveryStatus.NotSent, result.Status);
        Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task SenderFailure_ReturnsFailedWithoutThrowing()
    {
        var sender = new RecordingSender { Failure = new InvalidOperationException("provider down") };
        var service = new GiftCardDeliveryService(sender, new Configuration(true), NullLogger<GiftCardDeliveryService>.Instance);

        var result = await service.SendEmailAsync(Card(), "recipient@example.test", "KBeauty");

        Assert.Equal(GiftCardDeliveryStatus.Failed, result.Status);
        Assert.Empty(sender.Messages);
    }

    private static IssuedGiftCardDto Card(string sender = "Daniel", string message = "Felicidades")
    {
        var now = new DateTime(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);
        return new(new GiftCardDto(Guid.NewGuid(), "GC-AAAA-BBBB-CCCC", 500m, 500m, "MXN", GiftCardStatus.Active,
            null, "María López", "recipient@example.test", null, sender, message, GiftCardSource.Manual, now, now.AddMonths(12), now), "claim-token-123");
    }

    private sealed class Configuration(bool enabled) : IBillingEmailConfigurationProvider
    {
        public Task<BillingEmailSettingsDto> GetAsync(CancellationToken ct = default) => Task.FromResult(
            new BillingEmailSettingsDto(enabled, "Cloudflare", "notifications@example.test", "LoyaltyCloud", "https://admin.example.test", true, true));
    }

    private sealed class RecordingSender : ITransactionalEmailSender
    {
        public List<TransactionalEmail> Messages { get; } = [];
        public Exception? Failure { get; init; }
        public Task SendAsync(TransactionalEmail email, CancellationToken ct = default) { if (Failure is not null) throw Failure; Messages.Add(email); return Task.CompletedTask; }
    }
}
