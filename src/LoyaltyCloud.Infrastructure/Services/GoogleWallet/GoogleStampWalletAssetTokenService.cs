using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace LoyaltyCloud.Infrastructure.Services.GoogleWallet;

public sealed class GoogleStampWalletAssetTokenService
{
    private const string Purpose = "LoyaltyCloud.GoogleWallet.Stamps.ProgressAsset.v1";
    private readonly IDataProtector _protector;

    public GoogleStampWalletAssetTokenService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Create(Guid tenantId, string tenantSlug, string serialNumber)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(tenantSlug) || string.IsNullOrWhiteSpace(serialNumber))
            throw new ArgumentException("Tenant y serial son requeridos para el asset de Sellos.");

        var payload = JsonSerializer.Serialize(new StampAssetToken(
            tenantId,
            tenantSlug.Trim().ToLowerInvariant(),
            serialNumber.Trim().ToUpperInvariant()));
        return _protector.Protect(payload);
    }

    public bool TryRead(string token, out StampAssetToken value)
    {
        value = default!;
        if (string.IsNullOrWhiteSpace(token)) return false;
        try
        {
            value = JsonSerializer.Deserialize<StampAssetToken>(_protector.Unprotect(token))!;
            return value is not null && value.TenantId != Guid.Empty
                && !string.IsNullOrWhiteSpace(value.TenantSlug)
                && !string.IsNullOrWhiteSpace(value.SerialNumber);
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or JsonException)
        {
            return false;
        }
    }

    public sealed record StampAssetToken(Guid TenantId, string TenantSlug, string SerialNumber);
}
