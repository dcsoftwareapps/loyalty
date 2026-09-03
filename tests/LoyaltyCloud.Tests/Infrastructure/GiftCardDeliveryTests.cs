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
        Assert.Equal("KBeauty <store> te envió una tarjeta de regalo", email.Subject);
        Assert.Equal("recipient@example.test", email.Recipient);
        Assert.Equal("https://admin.example.test/giftcards/claim/claim-token-123", canonicalUrl);
        Assert.Contains(canonicalUrl!, email.TextBody);
        Assert.Contains("KBeauty &lt;store&gt;", email.HtmlBody);
        Assert.Contains("$500", email.TextBody);
        Assert.Contains("Ver mi tarjeta de regalo", email.HtmlBody);
        Assert.Contains("Daniel &lt;script&gt;", email.HtmlBody);
        Assert.Contains("Disfruta &amp; celebra", email.HtmlBody);
        Assert.DoesNotContain("<script>", email.HtmlBody);
    }

    [Fact]
    public async Task EnabledEmail_RendersPrimaryCtaBeforeCodeMessageAndExpiration()
    {
        var sender = new RecordingSender();
        var service = new GiftCardDeliveryService(sender, new Configuration(true), NullLogger<GiftCardDeliveryService>.Instance);

        await service.SendEmailAsync(Card(message: "Felicidades", expiresAtUtc: new DateTime(2026, 12, 25, 0, 0, 0, DateTimeKind.Utc)), "recipient@example.test", "K-Beauty");

        var email = Assert.Single(sender.Messages);
        var cta = email.TextBody.IndexOf("Ver mi tarjeta de regalo", StringComparison.Ordinal);
        var code = email.TextBody.IndexOf("Código:", StringComparison.Ordinal);
        var message = email.TextBody.IndexOf("Felicidades", StringComparison.Ordinal);
        var expiration = email.TextBody.IndexOf("Válida hasta:", StringComparison.Ordinal);
        Assert.True(cta > 0);
        Assert.True(cta < code);
        Assert.True(cta < message);
        Assert.True(cta < expiration);
        Assert.Contains("https://admin.example.test/giftcards/claim/claim-token-123", email.HtmlBody);
        Assert.Contains("https://admin.example.test/giftcards/claim/claim-token-123", email.TextBody);
    }

    [Theory]
    [InlineData("MXN", "500.00", "$500")]
    [InlineData("MXN", "1500.00", "$1,500")]
    [InlineData("MXN", "199.50", "$199.50")]
    [InlineData("USD", "199.50", "199.50 USD")]
    public async Task EnabledEmail_FormatsAmountForRecipientFacingGiftCardPresentation(string currency, string amountText, string expected)
    {
        var sender = new RecordingSender();
        var service = new GiftCardDeliveryService(sender, new Configuration(true), NullLogger<GiftCardDeliveryService>.Instance);

        await service.SendEmailAsync(Card(amount: decimal.Parse(amountText, System.Globalization.CultureInfo.InvariantCulture), currency: currency), "recipient@example.test", "KBeauty");

        var email = Assert.Single(sender.Messages);
        Assert.Contains(expected, email.TextBody);
        Assert.Contains(expected, email.HtmlBody);
    }

    [Fact]
    public async Task EnabledEmail_OmitsNoExpirationFillerButKeepsRealExpiration()
    {
        var noExpirationSender = new RecordingSender();
        var service = new GiftCardDeliveryService(noExpirationSender, new Configuration(true), NullLogger<GiftCardDeliveryService>.Instance);

        await service.SendEmailAsync(Card(expiresAtUtc: null), "recipient@example.test", "KBeauty");

        var noExpirationEmail = Assert.Single(noExpirationSender.Messages);
        Assert.DoesNotContain("Vigencia: sin expiración", noExpirationEmail.TextBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sin expiración", noExpirationEmail.HtmlBody, StringComparison.OrdinalIgnoreCase);

        var expiringSender = new RecordingSender();
        service = new GiftCardDeliveryService(expiringSender, new Configuration(true), NullLogger<GiftCardDeliveryService>.Instance);

        await service.SendEmailAsync(Card(expiresAtUtc: new DateTime(2026, 12, 25, 0, 0, 0, DateTimeKind.Utc)), "recipient@example.test", "KBeauty");

        var expiringEmail = Assert.Single(expiringSender.Messages);
        Assert.Contains("Válida hasta: 25/12/2026", expiringEmail.TextBody);
        Assert.Contains("25/12/2026", expiringEmail.HtmlBody);
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

    private static IssuedGiftCardDto Card(
        string sender = "Daniel",
        string message = "Felicidades",
        decimal amount = 500m,
        string currency = "MXN",
        DateTime? expiresAtUtc = null)
    {
        var now = new DateTime(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);
        return new(new GiftCardDto(Guid.NewGuid(), "GC-AAAA-BBBB-CCCC", amount, amount, currency, GiftCardStatus.Active,
            null, "María López", "recipient@example.test", null, sender, message, GiftCardSource.Manual, now, expiresAtUtc, now), "claim-token-123");
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
