using System.Security.Cryptography;
using System.Globalization;
using System.Text.Json;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.GiftCards;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class GiftCardAppleWalletService(
    AppDbContext db,
    IMutableTenantContext tenantContext,
    IDateTimeProvider clock,
    IApplePassPackageBuilder package,
    ITenantWalletAssetProvider assets,
    ITenantWalletBrandingReadService tenantWalletBranding,
    IApnService apn,
    IOptions<ApplePassOptions> options) : IGiftCardAppleWalletService
{
    private readonly ApplePassOptions _options=options.Value;

    public async Task<GiftCardApplePassResult> CreateOrUpdatePassAsync(Guid giftCardId, CancellationToken ct=default)
    {
        var card=await db.GiftCards.SingleOrDefaultAsync(x=>x.Id==giftCardId,ct)??throw new KeyNotFoundException("Tarjeta de regalo no encontrada.");
        var wallet=await db.GiftCardWallets.SingleOrDefaultAsync(x=>x.GiftCardId==giftCardId&&x.Provider==GiftCardWalletProvider.Apple,ct);
        if(wallet is null){var serial=$"GC-{card.TenantId:N}-{card.PublicCode.Replace("GC-","")}";var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));wallet=new GiftCardWallet(Guid.NewGuid(),card.TenantId,card.Id,GiftCardWalletProvider.Apple,_options.PassTypeIdentifier,serial,clock.UtcNow,token);db.GiftCardWallets.Add(wallet);await db.SaveChangesAsync(ct);}
        return await BuildAsync(card,wallet,ct);
    }

    public async Task<GiftCardApplePassResult?> GetPassAsync(string serialNumber,CancellationToken ct=default)
    {
        var wallet=await db.GiftCardWallets.IgnoreQueryFilters().SingleOrDefaultAsync(x=>x.Provider==GiftCardWalletProvider.Apple&&x.ExternalObjectId==serialNumber,ct);if(wallet is null)return null;
        if(!await SetTenantAsync(wallet.TenantId,ct))return null;var card=await db.GiftCards.SingleOrDefaultAsync(x=>x.Id==wallet.GiftCardId,ct);return card is null?null:await BuildAsync(card,wallet,ct);
    }

    public async Task<bool> AuthenticateAndSetTenantAsync(string serialNumber,string token,CancellationToken ct=default)
    {
        if(string.IsNullOrWhiteSpace(token))return false;var wallet=await db.GiftCardWallets.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x=>x.Provider==GiftCardWalletProvider.Apple&&x.ExternalObjectId==serialNumber,ct);
        if(wallet is null||!CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(wallet.AuthenticationToken??""),System.Text.Encoding.UTF8.GetBytes(token)))return false;
        return await SetTenantAsync(wallet.TenantId,ct);
    }

    public async Task<GiftCardAppleRegistrationResult> RegisterAsync(string deviceId,string passTypeId,string serialNumber,string pushToken,CancellationToken ct=default)
    {
        var wallet=await FindWalletAsync(serialNumber,ct);if(wallet is null||passTypeId!=_options.PassTypeIdentifier)return new(false,false);await SetTenantAsync(wallet.TenantId,ct);
        var existing=await db.GiftCardDeviceRegistrations.SingleOrDefaultAsync(x=>x.DeviceLibraryIdentifier==deviceId&&x.PassTypeIdentifier==passTypeId&&x.SerialNumber==serialNumber,ct);var isNew=existing is null;
        if(existing is null)db.GiftCardDeviceRegistrations.Add(new GiftCardDeviceRegistration(Guid.NewGuid(),wallet.TenantId,wallet.GiftCardId,deviceId,passTypeId,serialNumber,pushToken,clock.UtcNow));else existing.UpdatePushToken(pushToken,clock.UtcNow);await db.SaveChangesAsync(ct);return new(true,isNew);
    }

    public async Task<bool> UnregisterAsync(string deviceId,string passTypeId,string serialNumber,CancellationToken ct=default)
    {var wallet=await FindWalletAsync(serialNumber,ct);if(wallet is null)return false;await SetTenantAsync(wallet.TenantId,ct);var row=await db.GiftCardDeviceRegistrations.SingleOrDefaultAsync(x=>x.DeviceLibraryIdentifier==deviceId&&x.PassTypeIdentifier==passTypeId&&x.SerialNumber==serialNumber,ct);if(row is null)return true;db.Remove(row);await db.SaveChangesAsync(ct);return true;}

    public async Task<GiftCardAppleUpdates> GetUpdatesAsync(string deviceId,string passTypeId,DateTime? sinceUtc,CancellationToken ct=default)
    {var rows=await db.GiftCardDeviceRegistrations.IgnoreQueryFilters().AsNoTracking().Where(x=>x.DeviceLibraryIdentifier==deviceId&&x.PassTypeIdentifier==passTypeId).Join(db.GiftCardWallets.IgnoreQueryFilters(),r=>new{r.TenantId,r.GiftCardId},w=>new{w.TenantId,w.GiftCardId},(r,w)=>new{r.SerialNumber,w.UpdatedAtUtc}).Where(x=>sinceUtc==null||x.UpdatedAtUtc>sinceUtc).ToListAsync(ct);return new(rows.Select(x=>x.SerialNumber).Distinct().ToList(),rows.Count==0?clock.UtcNow:rows.Max(x=>x.UpdatedAtUtc));}

    public async Task SynchronizeAsync(Guid giftCardId,CancellationToken ct=default)
    {var wallet=await db.GiftCardWallets.SingleOrDefaultAsync(x=>x.GiftCardId==giftCardId&&x.Provider==GiftCardWalletProvider.Apple,ct);if(wallet is null)return;wallet.Pending(clock.UtcNow);await db.SaveChangesAsync(ct);var devices=await db.GiftCardDeviceRegistrations.AsNoTracking().Where(x=>x.GiftCardId==giftCardId).ToListAsync(ct);try{foreach(var d in devices){var result=await apn.SendPassUpdateAsync(d.PushToken,PassUpdateReason.RedemptionConfirmed,ct);if(!result.Success)throw new InvalidOperationException(result.Reason??"APNs rechazó la actualización.");}wallet.Synced(clock.UtcNow);}catch(Exception ex){wallet.Failed(ex.Message,clock.UtcNow);}await db.SaveChangesAsync(ct);}

    private async Task<GiftCardApplePassResult> BuildAsync(GiftCard card,GiftCardWallet wallet,CancellationToken ct)
    {
        var config=await db.GiftCardConfigurations.SingleAsync(ct);var tenant=await db.Tenants.AsNoTracking().SingleAsync(x=>x.Id==card.TenantId,ct);var branding=await tenantWalletBranding.GetForTenantAsync(card.TenantId,ct);
        var pass=new{formatVersion=1,passTypeIdentifier=_options.PassTypeIdentifier,serialNumber=wallet.ExternalObjectId,teamIdentifier=_options.TeamIdentifier,webServiceURL=_options.WebServiceURL,authenticationToken=wallet.AuthenticationToken,organizationName=tenant.DisplayName,description=$"{config.DisplayName} - {tenant.DisplayName}",backgroundColor=branding.BackgroundColor,foregroundColor=branding.ForegroundColor,labelColor=branding.LabelColor,storeCard=new{headerFields=new[]{new{key="gift_card_title",label=string.Empty,value="Tarjeta de regalo"}},primaryFields=new[]{new{key="balance",label=string.Empty,value=FormatBalance(card.CurrentBalance,card.Currency),changeMessage="Tu nuevo saldo es %@",textAlignment="PKTextAlignmentCenter"}},secondaryFields=BuildSecondaryFields(card),auxiliaryFields=Array.Empty<object>(),backFields=new[]{new{key="code",label="Código",value=card.PublicCode},new{key="terms",label="Términos",value=config.Terms??"Presenta este código al pagar"}}},barcodes=new[]{new{format="PKBarcodeFormatQR",message=card.PublicCode,messageEncoding="iso-8859-1",altText="Presenta este código al pagar"}}};
        var assetBytes=await assets.LoadAssetsAsync(branding.TenantId,branding.TenantSlug,branding.WalletLogoBlobName,branding.LogoBlobName,includeStripImage:false,stripImageBlobName:null,ct);var bytes=await package.BuildAsync(JsonSerializer.SerializeToUtf8Bytes(pass),assetBytes,ct);return new(bytes,wallet.ExternalObjectId,card.UpdatedAtUtc);
    }

    private static object[] BuildSecondaryFields(GiftCard card)
    {
        var fields = new List<object>();
        if (!string.IsNullOrWhiteSpace(card.SenderName))
            fields.Add(new { key="sender",label="DE",value=card.SenderName.Trim() });

        if (card.ExpiresAtUtc is { } expiresAtUtc)
            fields.Add(new { key="valid_until",label="VÁLIDA HASTA",value=expiresAtUtc.ToString("dd/MM/yyyy") });

        return fields.ToArray();
    }

    private static string FormatBalance(decimal amount,string currency)
    {
        var normalizedCurrency=string.IsNullOrWhiteSpace(currency)?"":currency.Trim().ToUpperInvariant();
        if (normalizedCurrency!="MXN")
            return $"{amount:N2} {normalizedCurrency}".Trim();

        var format=decimal.Truncate(amount)==amount?"#,0":"#,0.00";
        return "$"+amount.ToString(format,CultureInfo.InvariantCulture);
    }

    private Task<GiftCardWallet?> FindWalletAsync(string serial,CancellationToken ct)=>db.GiftCardWallets.IgnoreQueryFilters().SingleOrDefaultAsync(x=>x.Provider==GiftCardWalletProvider.Apple&&x.ExternalObjectId==serial,ct);
    private async Task<bool> SetTenantAsync(Guid tenantId,CancellationToken ct){var tenant=await db.Tenants.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==tenantId&&x.IsActive,ct);var subscription=await db.TenantSubscriptions.AsNoTracking().SingleOrDefaultAsync(x=>x.TenantId==tenantId,ct);if(tenant is null||subscription is null||!subscription.IsOperational(clock.UtcNow))return false;tenantContext.SetTenant(tenant.Id,tenant.Slug);return true;}
}
