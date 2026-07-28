namespace LoyaltyCloud.Infrastructure.Services.GoogleWallet;

public sealed record GoogleWalletObjectData(
    string Id,
    string ClassId,
    string AccountName,
    string AccountId,
    int PointsBalance,
    string MembershipTier,
    string BarcodeValue,
    bool IsActive,
    DateTime UpdatedAtUtc);

