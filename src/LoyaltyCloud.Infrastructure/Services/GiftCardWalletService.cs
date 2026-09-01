using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.GiftCards;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Services.GoogleWallet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class GiftCardWalletService(
    AppDbContext db,
    ITenantContext tenant,
    IGoogleWalletClient google,
    IGoogleWalletCredentialsProvider credentials,
    GoogleWalletJwtFactory jwt,
    IOptions<GoogleWalletOptions> options,
    IDateTimeProvider clock) : IGiftCardWalletService
{
    private readonly GoogleWalletOptions _options = options.Value;

    public async Task<GiftCardWalletLinkDto> GetGoogleSaveLinkAsync(Guid giftCardId, CancellationToken ct = default)
    {
        var card = await db.GiftCards.SingleOrDefaultAsync(x => x.Id == giftCardId, ct) ?? throw new KeyNotFoundException("Gift Card no encontrada.");
        var config = await db.GiftCardConfigurations.SingleOrDefaultAsync(x => x.IsEnabled, ct) ?? throw new InvalidOperationException("Gift Cards está deshabilitado.");
        var issuerId = string.IsNullOrWhiteSpace(_options.IssuerId) ? throw new InvalidOperationException("GoogleWallet:IssuerId no está configurado.") : _options.IssuerId.Trim();
        var tenantId = TenantId();
        var classId = $"{issuerId}.giftcard_{tenantId:N}";
        var objectId = $"{issuerId}.giftcard_{tenantId:N}_{card.PublicCode.Replace('-', '_').ToLowerInvariant()}";
        var record = await db.GiftCardWallets.SingleOrDefaultAsync(x => x.GiftCardId == card.Id && x.Provider == GiftCardWalletProvider.Google, ct);
        if (record is null) { record = new GiftCardWallet(Guid.NewGuid(), tenantId, card.Id, GiftCardWalletProvider.Google, classId, objectId, clock.UtcNow); db.GiftCardWallets.Add(record); }
        try
        {
            await google.EnsureGiftCardClassAsync(new GoogleGiftCardClassData(classId, config.DisplayName), ct);
            await google.CreateOrUpdateGiftCardObjectAsync(ToGoogleObject(card, config, classId, objectId), ct);
            record.Synced(clock.UtcNow); await db.SaveChangesAsync(ct);
            var account = await credentials.GetAsync(ct);
            var url = jwt.CreateGiftCardSaveUrl(account, objectId, classId, clock.UtcNow);
            return new(GiftCardWalletProvider.Google, url, classId, objectId);
        }
        catch (Exception ex)
        {
            record.Failed(ex.Message, clock.UtcNow); await db.SaveChangesAsync(ct); throw;
        }
    }

    public async Task SynchronizeAsync(Guid giftCardId, CancellationToken ct = default)
    {
        var wallet = await db.GiftCardWallets.SingleOrDefaultAsync(x => x.GiftCardId == giftCardId && x.Provider == GiftCardWalletProvider.Google, ct);
        if (wallet is null) return;
        var card = await db.GiftCards.SingleAsync(x => x.Id == giftCardId, ct);
        var config = await db.GiftCardConfigurations.SingleAsync(ct);
        wallet.Pending(clock.UtcNow); await db.SaveChangesAsync(ct);
        try { await google.CreateOrUpdateGiftCardObjectAsync(ToGoogleObject(card, config, wallet.ExternalClassId, wallet.ExternalObjectId), ct); wallet.Synced(clock.UtcNow); }
        catch (Exception ex) { wallet.Failed(ex.Message, clock.UtcNow); }
        await db.SaveChangesAsync(ct);
    }

    private static GoogleGiftCardObjectData ToGoogleObject(GiftCard card, GiftCardConfiguration config, string classId, string objectId) =>
        new(objectId, classId, config.DisplayName, card.RecipientName, card.PublicCode, card.CurrentBalance, card.Currency, card.Status.ToString(), config.PrimaryColor, config.LogoUrl, card.ExpiresAtUtc);
    private Guid TenantId() => tenant.TenantId is { } id && id != Guid.Empty ? id : throw new InvalidOperationException("Tenant requerido.");
}
