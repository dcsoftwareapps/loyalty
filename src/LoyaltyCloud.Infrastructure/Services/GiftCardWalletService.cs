using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.GiftCards;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Services.GoogleWallet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class GiftCardWalletService(
    AppDbContext db,
    IDbContextFactory<AppDbContext> dbContextFactory,
    ITenantContext tenant,
    IGoogleWalletClient google,
    IGoogleWalletCredentialsProvider credentials,
    GoogleWalletJwtFactory jwt,
    IOptions<GoogleWalletOptions> options,
    IDateTimeProvider clock,
    ILogger<GiftCardWalletService> logger) : IGiftCardWalletService
{
    private const int SyncBatchSize = 50;
    private readonly GoogleWalletOptions _options = options.Value;

    public async Task<GiftCardWalletLinkDto> GetGoogleSaveLinkAsync(Guid giftCardId, CancellationToken ct = default)
    {
        var tenantId = TenantId();
        var card = await db.GiftCards.SingleOrDefaultAsync(x => x.Id == giftCardId && x.TenantId == tenantId, ct) ?? throw new KeyNotFoundException("Tarjeta de regalo no encontrada.");
        var config = await db.GiftCardConfigurations.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.IsEnabled, ct) ?? throw new InvalidOperationException("El módulo de tarjetas de regalo está deshabilitado para este tenant.");
        var issuerId = string.IsNullOrWhiteSpace(_options.IssuerId) ? throw new InvalidOperationException("Google Wallet no está disponible.") : _options.IssuerId.Trim();
        var classId = $"{issuerId}.giftcard_{tenantId:N}";
        var objectId = $"{issuerId}.giftcard_{tenantId:N}_{card.PublicCode.Replace('-', '_').ToLowerInvariant()}";
        var record = await db.GiftCardWallets.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.GiftCardId == card.Id && x.Provider == GiftCardWalletProvider.Google, ct);
        if (record is null) { record = new GiftCardWallet(Guid.NewGuid(), tenantId, card.Id, GiftCardWalletProvider.Google, classId, objectId, clock.UtcNow); db.GiftCardWallets.Add(record); }
        try
        {
            await google.EnsureGiftCardClassAsync(new GoogleGiftCardClassData(classId, config.DisplayName), ct);
            await google.CreateOrUpdateGiftCardObjectAsync(ToGoogleObject(card, config, classId, objectId), ct);
            record.Synced(clock.UtcNow); await db.SaveChangesAsync(ct);
            var account = await credentials.GetAsync(ct);
            return new(GiftCardWalletProvider.Google, jwt.CreateGiftCardSaveUrl(account, objectId, classId, clock.UtcNow), classId, objectId);
        }
        catch (Exception ex)
        {
            record.Failed(ex.Message, clock.UtcNow); await db.SaveChangesAsync(ct); throw;
        }
    }

    public async Task SynchronizeAsync(Guid giftCardId, CancellationToken ct = default)
    {
        var tenantId = TenantId();
        var wallet = await db.GiftCardWallets.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.GiftCardId == giftCardId && x.Provider == GiftCardWalletProvider.Google, ct);
        if (wallet is null) return;
        var card = await db.GiftCards.SingleAsync(x => x.TenantId == tenantId && x.Id == giftCardId, ct);
        var config = await db.GiftCardConfigurations.SingleAsync(x => x.TenantId == tenantId, ct);
        wallet.Pending(clock.UtcNow); await db.SaveChangesAsync(ct);
        try { await google.EnsureGiftCardClassAsync(new(wallet.ExternalClassId, config.DisplayName), ct); await google.CreateOrUpdateGiftCardObjectAsync(ToGoogleObject(card, config, wallet.ExternalClassId, wallet.ExternalObjectId), ct); wallet.Synced(clock.UtcNow); }
        catch (Exception ex) { wallet.Failed(ex.Message, clock.UtcNow); logger.LogWarning(ex, "Google Gift Card sync failed. TenantId={TenantId}, GiftCardId={GiftCardId}", tenantId, giftCardId); }
        await db.SaveChangesAsync(ct);
    }

    public async Task<GiftCardWalletSyncResult> SynchronizeBrandingAsync(CancellationToken ct = default)
    {
        var tenantId = TenantId();
        await using var readDb = await dbContextFactory.CreateDbContextAsync(ct);
        var config = await readDb.GiftCardConfigurations.AsNoTracking().SingleAsync(x => x.TenantId == tenantId, ct);
        var mappings = await readDb.GiftCardWallets.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Provider == GiftCardWalletProvider.Google)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.GiftCardId, x.ExternalClassId, x.ExternalObjectId })
            .ToListAsync(ct);
        if (mappings.Count == 0) return new(0, 0);

        var failed = 0;
        foreach (var classGroup in mappings.GroupBy(x => x.ExternalClassId))
        {
            try { await google.EnsureGiftCardClassAsync(new(classGroup.Key, config.DisplayName), ct); }
            catch (Exception ex) { failed += classGroup.Count(); logger.LogWarning(ex, "Google Gift Card class update failed. TenantId={TenantId}, ClassId={ClassId}", tenantId, classGroup.Key); continue; }

            foreach (var batch in classGroup.Chunk(SyncBatchSize))
            {
                var cardIds = batch.Select(x => x.GiftCardId).ToArray();
                await using var batchDb = await dbContextFactory.CreateDbContextAsync(ct);
                var cards = await batchDb.GiftCards.AsNoTracking()
                    .Where(x => x.TenantId == tenantId && cardIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, ct);
                foreach (var mapping in batch)
                {
                    if (!cards.TryGetValue(mapping.GiftCardId, out var card)) continue;
                    try { await google.CreateOrUpdateGiftCardObjectAsync(ToGoogleObject(card, config, mapping.ExternalClassId, mapping.ExternalObjectId), ct); }
                    catch (Exception ex) { failed++; logger.LogWarning(ex, "Google Gift Card branding sync failed. TenantId={TenantId}, GiftCardId={GiftCardId}", tenantId, mapping.GiftCardId); }
                }
            }
        }
        return new(mappings.Count, failed);
    }

    private static GoogleGiftCardObjectData ToGoogleObject(GiftCard card, GiftCardConfiguration config, string classId, string objectId) =>
        new(objectId, classId, config.DisplayName, card.RecipientName, card.PublicCode, card.CurrentBalance, card.Currency, card.Status.ToString(), config.PrimaryColor, config.LogoUrl, config.LogoUrl, card.ExpiresAtUtc);

    private Guid TenantId() => tenant.TenantId is { } id && id != Guid.Empty ? id : throw new InvalidOperationException("Tenant requerido.");
}
