using LoyaltyCloud.Application.Billing;
using LoyaltyCloud.Application.GiftCards;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Services;
using Xunit;

namespace LoyaltyCloud.Tests.Infrastructure;

public sealed class GiftCardDeliveryTests
{
    [Fact]
    public async Task DisabledEmail_DoesNotCallSender()
    {
        var sender = new RecordingSender();
        var service = new GiftCardDeliveryService(sender, new Configuration(false));

        await service.SendEmailAsync(Card(), "recipient@example.test");

        Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task EnabledEmail_ContainsSenderMessageAndCorrectClaimUrl_WithHtmlEncoding()
    {
        var sender = new RecordingSender();
        var service = new GiftCardDeliveryService(sender, new Configuration(true));

        var canonicalUrl = await service.GetClaimUrlAsync("claim-token-123");
        await service.SendEmailAsync(Card("Daniel <script>", "Disfruta & celebra"), "recipient@example.test");

        var email = Assert.Single(sender.Messages);
        Assert.Equal("https://admin.example.test/giftcards/claim/claim-token-123", canonicalUrl);
        Assert.Contains(canonicalUrl!, email.TextBody);
        Assert.Contains("Daniel &lt;script&gt;", email.HtmlBody);
        Assert.Contains("Disfruta &amp; celebra", email.HtmlBody);
        Assert.DoesNotContain("<script>", email.HtmlBody);
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
        public Task SendAsync(TransactionalEmail email, CancellationToken ct = default) { Messages.Add(email); return Task.CompletedTask; }
    }
}
