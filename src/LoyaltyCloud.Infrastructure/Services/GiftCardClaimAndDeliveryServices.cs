using System.Net;
using LoyaltyCloud.Application.Billing;
using LoyaltyCloud.Application.GiftCards;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class GiftCardClaimService(AppDbContext db, IDateTimeProvider clock) : IGiftCardClaimService
{
    public async Task<GiftCardClaimDto?> GetAsync(string claimToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(claimToken) || claimToken.Length > 256) return null;
        var hash = GiftCard.HashClaimToken(claimToken);
        var card = await db.GiftCards.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.ClaimTokenHash == hash && !x.ClaimRevoked, ct);
        if (card is null) return null;
        var config = await db.GiftCardConfigurations.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == card.TenantId && x.IsEnabled, ct);
        if (config is null) return null;
        var status = card.Status == GiftCardStatus.Active && card.ExpiresAtUtc <= clock.UtcNow ? GiftCardStatus.Expired : card.Status;
        var dto = new GiftCardDto(card.Id, card.PublicCode, card.InitialValue, card.CurrentBalance, card.Currency, status, card.RecipientMemberId, card.RecipientName, card.RecipientEmail, card.RecipientPhone, card.SenderName, card.PersonalMessage, card.Source, card.IssuedAtUtc, card.ExpiresAtUtc, card.UpdatedAtUtc);
        return new(dto, config.DisplayName, config.PrimaryColor, config.TextColor, config.LogoUrl, config.SecondaryText, config.Terms, config.FooterMessage);
    }
}

internal sealed class GiftCardDeliveryService(ITransactionalEmailSender sender, IBillingEmailConfigurationProvider emailConfiguration) : IGiftCardDeliveryService
{
    public async Task SendEmailAsync(IssuedGiftCardDto giftCard, string recipient, CancellationToken ct = default)
    {
        var settings = await emailConfiguration.GetAsync(ct);
        if (!settings.IsComplete || !settings.Enabled || string.IsNullOrWhiteSpace(settings.FromAddress) || string.IsNullOrWhiteSpace(settings.ApplicationBaseUrl))
            return;
        if (string.IsNullOrWhiteSpace(recipient)) throw new ArgumentException("Email de destinatario requerido.");
        var url = $"{settings.ApplicationBaseUrl.TrimEnd('/')}/giftcards/claim/{Uri.EscapeDataString(giftCard.ClaimToken)}";
        var name = WebUtility.HtmlEncode(giftCard.Card.RecipientName);
        var code = WebUtility.HtmlEncode(giftCard.Card.Code);
        var amount = $"{giftCard.Card.CurrentBalance:N2} {giftCard.Card.Currency}";
        var senderName = WebUtility.HtmlEncode(giftCard.Card.SenderName);
        var personalMessage = WebUtility.HtmlEncode(giftCard.Card.PersonalMessage);
        var text = $"Hola {giftCard.Card.RecipientName}, recibiste una Gift Card por {amount}. De: {giftCard.Card.SenderName}. Mensaje: {giftCard.Card.PersonalMessage}. Código: {giftCard.Card.Code}. Consultar: {url}";
        var html = $"<h1>Recibiste una Gift Card</h1><p>Hola {name},</p><p>Tu saldo inicial es <strong>{amount}</strong>.</p><p>De: <strong>{senderName}</strong></p><p>{personalMessage}</p><p>Código: <strong>{code}</strong></p><p><a href=\"{WebUtility.HtmlEncode(url)}\">Ver mi Gift Card</a></p>";
        await sender.SendAsync(new TransactionalEmail(recipient.Trim(), "Recibiste una Gift Card", text, html, settings.FromAddress, settings.FromName), ct);
    }
}
