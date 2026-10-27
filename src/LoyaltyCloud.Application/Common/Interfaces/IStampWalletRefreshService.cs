using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Application.Common.Interfaces;

public interface IStampWalletRefreshService
{
    Task RefreshCardAsync(
        Guid tenantId,
        Guid loyaltyCardId,
        string serialNumber,
        PassUpdateReason reason,
        CancellationToken ct = default);
}
