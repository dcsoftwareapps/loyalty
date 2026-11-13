using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class StampWalletRefreshService(
    IAppleWalletPassRefreshService apple,
    IGoogleStampWalletService google,
    ILogger<StampWalletRefreshService> logger) : IStampWalletRefreshService
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
            logger.LogWarning(ex, "Apple Wallet Stamps refresh failed after commit. TenantId={TenantId}, Serial={Serial}", tenantId, serialNumber);
        }

        try
        {
            await google.SynchronizeBySerialNumberIfExistsAsync(serialNumber, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Google Wallet Stamps refresh failed after commit. TenantId={TenantId}, Serial={Serial}", tenantId, serialNumber);
        }
    }
}
