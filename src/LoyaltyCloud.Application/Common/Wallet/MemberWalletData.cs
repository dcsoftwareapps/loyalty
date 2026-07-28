namespace LoyaltyCloud.Application.Common.Wallet;

/// <summary>
/// Provider-neutral projection of the loyalty data needed by digital wallets.
/// It intentionally contains no Apple or Google protocol concepts.
/// </summary>
public sealed record MemberWalletData(
    Guid TenantId,
    Guid CustomerId,
    Guid LoyaltyCardId,
    string SerialNumber,
    string FullName,
    string? Email,
    string? Phone,
    int CurrentPoints,
    int LifetimePoints,
    string Level,
    DateTime LevelAchievedAt,
    DateTime LastActivityAt,
    bool IsActive,
    string BarcodeValue);

