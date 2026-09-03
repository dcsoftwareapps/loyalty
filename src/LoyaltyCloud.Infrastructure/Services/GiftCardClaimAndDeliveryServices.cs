using System.Net;
using System.Globalization;
using LoyaltyCloud.Application.Billing;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.GiftCards;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class GiftCardClaimService(
    AppDbContext db,
    IDateTimeProvider clock,
    IMutableTenantContext tenantContext,
    ITenantBrandingReadService tenantBranding,
    ITenantBrandingLogoUrlProvider logoUrls,
    IGiftCardWalletService googleWallet,
    IGiftCardAppleWalletService appleWallet) : IGiftCardClaimService
{
    public async Task<GiftCardClaimDto?> GetAsync(string claimToken, CancellationToken ct = default)
    {
        var resolved = await ResolveAsync(claimToken, ct);
        if (resolved is null) return null;
        var (card, slug) = resolved.Value;
        tenantContext.SetTenant(card.TenantId, slug);
        var tenantPresentation = await tenantBranding.GetCurrentAsync(ct);
        var config = await db.GiftCardConfigurations.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == card.TenantId && x.IsEnabled, ct);
        if (config is null) return null;
        var status = card.Status == GiftCardStatus.Active && card.ExpiresAtUtc <= clock.UtcNow ? GiftCardStatus.Expired : card.Status;
        var dto = new GiftCardDto(card.Id, card.PublicCode, card.InitialValue, card.CurrentBalance, card.Currency, status, card.RecipientMemberId, card.RecipientName, card.RecipientEmail, card.RecipientPhone, card.SenderName, card.PersonalMessage, card.Source, card.IssuedAtUtc, card.ExpiresAtUtc, card.UpdatedAtUtc);
        var logoUrl = ResolveLogoDisplayUrl(config.LogoUrl);
        var effective = GiftCardBrandingResolver.Resolve(config.PrimaryColor, config.TextColor, config.DisplayName, logoUrl, tenantPresentation.ResolvedWalletBackgroundColor, tenantPresentation.DisplayName, tenantPresentation.WalletLogoUrl ?? tenantPresentation.LogoUrl);
        return new(dto, effective.DisplayName, effective.BackgroundColor, effective.TextColor, effective.LogoUrl, config.SecondaryText, config.Terms, config.FooterMessage);
    }

    public async Task<GiftCardApplePassResult> GetApplePassAsync(string claimToken, CancellationToken ct = default)
    {
        var (card, slug) = await RequireActiveAsync(claimToken, ct);
        tenantContext.SetTenant(card.TenantId, slug);
        return await appleWallet.CreateOrUpdatePassAsync(card.Id, ct);
    }

    public async Task<GiftCardWalletLinkDto> GetGoogleWalletLinkAsync(string claimToken, CancellationToken ct = default)
    {
        var (card, slug) = await RequireActiveAsync(claimToken, ct);
        tenantContext.SetTenant(card.TenantId, slug);
        return await googleWallet.GetGoogleSaveLinkAsync(card.Id, ct);
    }

    private async Task<(GiftCard Card, string Slug)> RequireActiveAsync(string token, CancellationToken ct)
    {
        var resolved = await ResolveAsync(token, ct) ?? throw new KeyNotFoundException("Esta tarjeta de regalo no está disponible.");
        var (card, slug) = resolved;
        card.EvaluateExpiration(clock.UtcNow);
        if (card.Status != GiftCardStatus.Active) throw new InvalidOperationException("Esta tarjeta de regalo ya no está disponible para Wallet.");
        return (card, slug);
    }

    private async Task<(GiftCard Card, string Slug)?> ResolveAsync(string claimToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(claimToken) || claimToken.Length > 256) return null;
        var hash = GiftCard.HashClaimToken(claimToken);
        var card = await db.GiftCards.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.ClaimTokenHash == hash && !x.ClaimRevoked, ct);
        if (card is null) return null;
        var tenant = await db.Tenants.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Id == card.TenantId && x.IsActive, ct);
        return tenant is null ? null : (card, tenant.Slug);
    }

    private string? ResolveLogoDisplayUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : logoUrls.GetDisplayUrl(trimmed);
    }
}

internal sealed class GiftCardDeliveryService(ITransactionalEmailSender sender, IBillingEmailConfigurationProvider emailConfiguration, ILogger<GiftCardDeliveryService> logger) : IGiftCardDeliveryService
{
    public async Task<string?> GetClaimUrlAsync(string claimToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(claimToken)) return null;
        var settings = await emailConfiguration.GetAsync(ct);
        return string.IsNullOrWhiteSpace(settings.ApplicationBaseUrl)
            ? null
            : $"{settings.ApplicationBaseUrl.TrimEnd('/')}/giftcards/claim/{Uri.EscapeDataString(claimToken.Trim())}";
    }

    public async Task<GiftCardDeliveryResult> SendEmailAsync(IssuedGiftCardDto giftCard, string recipient, string businessName, CancellationToken ct = default)
    {
        var settings = await emailConfiguration.GetAsync(ct);
        if (!settings.IsComplete || !settings.Enabled || string.IsNullOrWhiteSpace(settings.FromAddress) || string.IsNullOrWhiteSpace(settings.ApplicationBaseUrl))
            return new(GiftCardDeliveryStatus.NotSent, "Email no enviado: la configuración de email está deshabilitada o incompleta.", null);
        if (string.IsNullOrWhiteSpace(recipient))
            return new(GiftCardDeliveryStatus.NotSent, "Email no enviado: falta el email del destinatario.", null);

        var url = await GetClaimUrlAsync(giftCard.ClaimToken, ct);
        if (string.IsNullOrWhiteSpace(url))
            return new(GiftCardDeliveryStatus.NotSent, "Email no enviado: falta el enlace público de entrega.", null);

        var displayName = string.IsNullOrWhiteSpace(businessName) ? "LoyaltyCloud" : businessName.Trim();
        var subject = $"{displayName} te envió una tarjeta de regalo";
        var name = WebUtility.HtmlEncode(giftCard.Card.RecipientName);
        var code = WebUtility.HtmlEncode(giftCard.Card.Code);
        var business = WebUtility.HtmlEncode(displayName);
        var amount = FormatMoney(giftCard.Card.CurrentBalance, giftCard.Card.Currency);
        var senderLine = string.IsNullOrWhiteSpace(giftCard.Card.SenderName) ? null : $"De: {giftCard.Card.SenderName}.";
        var messageLine = string.IsNullOrWhiteSpace(giftCard.Card.PersonalMessage) ? null : $"Mensaje: {giftCard.Card.PersonalMessage}.";
        var expiresLine = giftCard.Card.ExpiresAtUtc is null ? null : $"Válida hasta: {giftCard.Card.ExpiresAtUtc.Value:dd/MM/yyyy}.";
        var text = $"Hola {giftCard.Card.RecipientName}, {displayName} te envió una tarjeta de regalo por {amount}. {senderLine} Ver mi tarjeta de regalo: {url}. Código: {giftCard.Card.Code}. {messageLine} {expiresLine}".Trim();
        var html = $"<h1>{business} te envió una tarjeta de regalo</h1><p>Hola {name},</p><p>Recibiste una tarjeta de regalo por <strong>{WebUtility.HtmlEncode(amount)}</strong>.</p>{HtmlParagraph("De:", giftCard.Card.SenderName)}<p><a href=\"{WebUtility.HtmlEncode(url)}\">Ver mi tarjeta de regalo</a></p><p>Código: <strong>{code}</strong></p>{HtmlParagraph(null, giftCard.Card.PersonalMessage)}{HtmlParagraph(null, expiresLine)}";

        try
        {
            await sender.SendAsync(new TransactionalEmail(recipient.Trim(), subject, text, html, settings.FromAddress, settings.FromName), ct);
            return new(GiftCardDeliveryStatus.Sent, "Email enviado correctamente.", url);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Gift Card email delivery failed for card {GiftCardId}.", giftCard.Card.Id);
            return new(GiftCardDeliveryStatus.Failed, "No pudimos enviar el email. Verifica la configuración de email e inténtalo nuevamente.", url);
        }
    }

    private static string HtmlParagraph(string? prefix, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var label = string.IsNullOrWhiteSpace(prefix) ? string.Empty : $"<strong>{WebUtility.HtmlEncode(prefix.Trim())}</strong> ";
        return $"<p>{label}{WebUtility.HtmlEncode(value.Trim())}</p>";
    }

    private static string FormatMoney(decimal amount, string currency)
    {
        var normalizedCurrency = string.IsNullOrWhiteSpace(currency)
            ? "MXN"
            : currency.Trim().ToUpperInvariant();
        var normalizedAmount = decimal.Round(amount, 2);
        var format = decimal.Truncate(normalizedAmount) == normalizedAmount ? "N0" : "N2";
        return normalizedCurrency == "MXN"
            ? $"${normalizedAmount.ToString(format, CultureInfo.InvariantCulture)}"
            : $"{normalizedAmount.ToString(format, CultureInfo.InvariantCulture)} {normalizedCurrency}";
    }
}
