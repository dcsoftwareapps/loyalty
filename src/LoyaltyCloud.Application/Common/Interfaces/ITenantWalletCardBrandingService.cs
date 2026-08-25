using LoyaltyCloud.Common.Results;

namespace LoyaltyCloud.Application.Common.Interfaces;

public interface ITenantWalletCardBrandingService
{
    Task<Result<TenantBrandingInfo>> UpdateBackgroundColorAsync(
        string? walletBackgroundColor,
        CancellationToken cancellationToken = default);

    Task RefreshInstalledApplePassesBestEffortAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
