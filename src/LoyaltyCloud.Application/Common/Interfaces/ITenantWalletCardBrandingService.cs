using LoyaltyCloud.Common.Results;

namespace LoyaltyCloud.Application.Common.Interfaces;

public interface ITenantWalletCardBrandingService
{
    Task<Result<TenantBrandingInfo>> UpdateAsync(
        string? walletBackgroundColor,
        int? walletLogoScalePercent,
        CancellationToken cancellationToken = default);

    Task RefreshInstalledApplePassesBestEffortAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
