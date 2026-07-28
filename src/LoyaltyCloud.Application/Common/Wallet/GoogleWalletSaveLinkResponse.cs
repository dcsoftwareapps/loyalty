namespace LoyaltyCloud.Application.Common.Wallet;

public sealed record GoogleWalletSaveLinkResponse(
    string SaveUrl,
    string ObjectId,
    string ClassId,
    DateTime? LastSynchronizedAt);

