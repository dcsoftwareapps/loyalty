using System.Security.Cryptography;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Common.Wallet;
using LoyaltyCloud.Application.Stamps;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoyaltyCloud.Infrastructure.Services.GoogleWallet;

internal sealed class GoogleStampWalletService(
    AppDbContext db,
    ITenantContext tenant,
    IStampWalletProjectionService projections,
    ITenantWalletBrandingReadService branding,
    IMemberDigitalWalletRepository wallets,
    IUnitOfWork uow,
    IGoogleWalletClient client,
    IGoogleWalletCredentialsProvider credentials,
    GoogleWalletIdGenerator ids,
    GoogleStampWalletMapper mapper,
    GoogleStampWalletAssetTokenService assetTokens,
    GoogleWalletJwtFactory jwt,
    IDateTimeProvider clock,
    IOptions<GoogleWalletOptions> options,
    ILogger<GoogleStampWalletService> logger) : IGoogleStampWalletService
{
    private readonly GoogleWalletOptions _options = options.Value;

    public async Task<Result<GoogleWalletSaveLinkResponse>> GetOrCreateSaveLinkAsync(
        string serialNumber, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Result.Fail<GoogleWalletSaveLinkResponse>("Google Wallet esta deshabilitado.");

        var configurationErrors = _options.ValidateForEnabled();
        if (configurationErrors.Count > 0)
            return Result.Fail<GoogleWalletSaveLinkResponse>(configurationErrors[0]);

        try
        {
            var state = await SynchronizeCoreAsync(serialNumber, createIfMissing: true, ct);
            if (state is null)
                return Result.Fail<GoogleWalletSaveLinkResponse>("No se encontro una tarjeta activa de Sellos.");

            var key = await credentials.GetAsync(ct);
            var saveUrl = jwt.CreateStampSaveUrl(key, state.Object, clock.UtcNow);
            state.Wallet.RecordSaveLinkCreated(clock.UtcNow);
            wallets.Update(state.Wallet);
            await uow.SaveChangesAsync(ct);
            return Result.Ok(new GoogleWalletSaveLinkResponse(
                saveUrl,
                state.Wallet.ExternalObjectId,
                state.Wallet.ExternalClassId,
                state.Wallet.LastSynchronizedAt));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Google Wallet Stamps save link failed. Serial={Serial}", serialNumber);
            return Result.Fail<GoogleWalletSaveLinkResponse>(
                "No se pudo generar el enlace de Google Wallet para Sellos.");
        }
    }

    public async Task SynchronizeBySerialNumberIfExistsAsync(string serialNumber, CancellationToken ct = default)
    {
        if (!_options.Enabled) return;
        try
        {
            await SynchronizeCoreAsync(serialNumber, createIfMissing: false, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Google Wallet Stamps sync failed. Serial={Serial}", serialNumber);
        }
    }

    private async Task<SyncState?> SynchronizeCoreAsync(
        string serialNumber, bool createIfMissing, CancellationToken ct)
    {
        var configurationErrors = _options.ValidateForEnabled();
        if (configurationErrors.Count > 0)
            throw new InvalidOperationException(configurationErrors[0]);

        var normalized = serialNumber?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        var tenantId = tenant.RequireTenantId();
        var member = await db.LoyaltyCards.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.SerialNumber == normalized && x.IsActive)
            .Join(db.Customers.AsNoTracking().Where(x => x.IsActive), card => card.CustomerId, customer => customer.Id,
                (card, customer) => new
                {
                    CardId = card.Id,
                    CustomerId = customer.Id,
                    card.SerialNumber,
                    customer.FullName,
                    card.LastActivityAt
                })
            .SingleOrDefaultAsync(ct);
        if (member is null) return null;

        var wallet = await wallets.GetByLoyaltyCardAndProviderAsync(member.CardId, DigitalWalletProvider.Google, ct);
        if (wallet is null && !createIfMissing) return null;

        var classId = wallet?.ExternalClassId ?? ids.BuildStampClassId(_options, tenantId);
        var objectId = wallet?.ExternalObjectId ?? ids.BuildStampObjectId(_options, tenantId, member.SerialNumber);
        var progressAssetToken = assetTokens.Create(tenantId, tenant.TenantSlug!, member.SerialNumber);
        var now = clock.UtcNow;

        if (wallet is null)
        {
            wallet = new MemberDigitalWallet(Guid.NewGuid(), tenantId, member.CustomerId, member.CardId,
                DigitalWalletProvider.Google, classId, objectId, now);
            await wallets.AddAsync(wallet, ct);
        }

        GoogleStampWalletObjectData? objectData = null;

        try
        {
            const int maxStabilizationAttempts = 3;
            for (var attempt = 1; attempt <= maxStabilizationAttempts; attempt++)
            {
                var effectiveBranding = await branding.GetCurrentAsync(ct);
                var projection = await projections.ReadAsync(member.SerialNumber, ct);
                var imageUri = BuildProgressImageUri(effectiveBranding, progressAssetToken, projection);
                objectData = mapper.ToObjectData(objectId, classId, member.FullName, member.SerialNumber,
                    true, member.LastActivityAt, projection, imageUri);

                await client.EnsureStampLoyaltyClassAsync(
                    mapper.ToClassData(classId, _options, effectiveBranding), ct);
                await client.CreateOrUpdateStampObjectAsync(objectData, notifyOnUpdate: !createIfMissing, ct);

                var latestBranding = await branding.GetCurrentAsync(ct);
                var latestProjection = await projections.ReadAsync(member.SerialNumber, ct);
                var latest = mapper.ToObjectData(objectId, classId, member.FullName, member.SerialNumber,
                    true, member.LastActivityAt, latestProjection,
                    BuildProgressImageUri(latestBranding, progressAssetToken, latestProjection));
                if (objectData == latest)
                    break;
                if (attempt == maxStabilizationAttempts)
                    throw new InvalidOperationException(
                        "El estado de Sellos cambio repetidamente durante la sincronizacion de Google Wallet.");
            }
            wallet.MarkSynchronized(now);
        }
        catch (Exception ex)
        {
            wallet.MarkSynchronizationFailed(SafeError(ex), now);
            wallets.Update(wallet);
            await uow.SaveChangesAsync(ct);
            throw;
        }

        wallets.Update(wallet);
        await uow.SaveChangesAsync(ct);
        return new(wallet, objectData!);
    }

    private string? BuildProgressImageUri(
        TenantWalletBrandingDto tenantBranding,
        string progressAssetToken,
        StampWalletProjection projection)
    {
        var configured = string.IsNullOrWhiteSpace(_options.LogoUri) ? _options.WideLogoUri : _options.LogoUri;
        if (!Uri.TryCreate(configured, UriKind.Absolute, out var origin)
            || origin.Scheme != Uri.UriSchemeHttps || origin.IsLoopback) return null;
        var token = Uri.EscapeDataString(progressAssetToken);
        var strip = projections.Render(projection, tenantBranding.BackgroundHex)
            .Single(x => x.Name == "strip@3x.png");
        var contentHash = Convert.ToHexString(SHA256.HashData(strip.Bytes))[..20].ToLowerInvariant();
        var version = $"{projection.CycleId:N}-{contentHash}";
        return new Uri(origin, $"/api/wallet-assets/google/stamps/progress/{token}.png?v={Uri.EscapeDataString(version)}").ToString();
    }

    private static string SafeError(Exception ex) =>
        ex.Message.Length <= 500 ? ex.Message : ex.Message[..500];

    private sealed record SyncState(MemberDigitalWallet Wallet, GoogleStampWalletObjectData Object);
}
