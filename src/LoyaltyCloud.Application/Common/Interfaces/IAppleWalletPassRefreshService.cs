using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Application.Common.Interfaces;

public interface IAppleWalletPassRefreshService
{
    Task<AppleWalletPassRefreshResult> RefreshCardAsync(
        Guid tenantId,
        Guid loyaltyCardId,
        PassUpdateReason reason,
        CancellationToken ct = default);

    Task<AppleWalletPassRefreshResult> RefreshTenantInstalledPassesAsync(
        Guid tenantId,
        PassUpdateReason reason,
        CancellationToken ct = default);
}

public sealed record AppleWalletPassRefreshResult(
    Guid TenantId,
    IReadOnlyList<string> SerialNumbers,
    int CardsTouched,
    int DevicesFound,
    int PushesAttempted,
    int PushesAccepted,
    int PushesFailed,
    bool Unsupported,
    ApnPushFailureType WorstFailureType,
    string? FailureReason)
{
    public bool HasRecipients => DevicesFound > 0;
    public bool AllAccepted => HasRecipients && !Unsupported && PushesAttempted > 0 && PushesFailed == 0;
}
