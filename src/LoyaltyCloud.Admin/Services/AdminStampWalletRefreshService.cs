using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Common.Wallet;
using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Admin.Services;

// Cashier runs in Admin, while Google Wallet credentials and object updates live in API.
public sealed class AdminStampWalletRefreshService(
    IAppleWalletPassRefreshService apple,
    AdminApiClient api,
    ILogger<AdminStampWalletRefreshService> logger) : IStampWalletRefreshService
{
    public async Task RefreshCardAsync(Guid tenantId, Guid loyaltyCardId, string serialNumber,
        PassUpdateReason reason, CancellationToken ct = default)
    {
        try
        {
            await apple.RefreshCardAsync(tenantId, loyaltyCardId, reason, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Apple Wallet Stamps refresh failed after commit. TenantId={TenantId}, Serial={Serial}",
                tenantId, serialNumber);
        }

        try
        {
            var path = $"/api/internal/stamps/google/{Uri.EscapeDataString(serialNumber)}/sync";
            var result = await api.PostAsync<GoogleStampWalletSyncResponse>(path, ct);
            if (result.IsFailure)
                logger.LogWarning("Google Wallet Stamps refresh failed after commit. TenantId={TenantId}, Serial={Serial}, Error={Error}",
                    tenantId, serialNumber, result.Error);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Google Wallet Stamps refresh failed after commit. TenantId={TenantId}, Serial={Serial}",
                tenantId, serialNumber);
        }
    }
}
