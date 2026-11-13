namespace LoyaltyCloud.Infrastructure.Services.GoogleWallet;

public sealed record GoogleStampWalletClassData(
    string Id,
    string ProgramName,
    string IssuerName,
    string? LogoUri,
    string? WideLogoUri,
    string? HexBackgroundColor);

public sealed record GoogleStampWalletObjectData(
    string Id,
    string ClassId,
    string AccountName,
    string AccountId,
    int CurrentStamps,
    int MaxStamps,
    int CycleSequence,
    string ProgressText,
    string RemainingText,
    string NextRewardText,
    string AvailableRewardsText,
    string RewardHistoryText,
    string BarcodeValue,
    string BarcodeAlternateText,
    string? ProgressImageUri,
    bool IsActive,
    DateTime UpdatedAtUtc);
