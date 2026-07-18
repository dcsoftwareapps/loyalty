namespace KBeauty.Loyalty.Application.Common.Interfaces;

public interface IWalletNotificationReadService
{
    Task<WalletNotificationMessage?> GetActiveMessageAsync(Guid loyaltyCardId, CancellationToken ct = default);
    Task<WalletNotificationContext> GetActiveContextAsync(Guid loyaltyCardId, CancellationToken ct = default);
}

public sealed record WalletNotificationContext(
    WalletNotificationMessage? News,
    WalletNotificationMessage? LevelChange,
    WalletPointsExpiringMessage? PointsExpiring,
    WalletMonthlyProductMessage? MonthlyProduct,
    WalletBirthdayBenefitMessage? BirthdayBenefit,
    WalletPointCampaignMessage? PointCampaign,
    WalletCustomMessage? CustomMessage,
    WalletRecentVisibleEvent? RecentVisibleEvent);

public sealed record WalletNotificationMessage(
    Guid Id,
    KBeauty.Loyalty.Domain.Enums.NotificationType Type,
    string Title,
    string Message,
    string? MetadataJson);

public sealed record WalletPointsExpiringMessage(
    Guid NotificationId,
    int Points,
    DateOnly ExpirationDate,
    string Value,
    string ChangeMessage,
    string Message);

public sealed record WalletMonthlyProductMessage(
    Guid RewardId,
    string ProductName,
    int PointsCost,
    DateTime ValidToUtc,
    DateOnly ValidToLocalDate,
    string Value,
    string ChangeMessage,
    string BackValue);

public sealed record WalletBirthdayBenefitMessage(
    int BenefitYear,
    int Multiplier,
    DateOnly DisplayUntilLocalDate,
    string Value,
    string ChangeMessage,
    string BackValue);

public sealed record WalletPointCampaignMessage(
    Guid NotificationId,
    Guid CampaignId,
    string CampaignName,
    int Multiplier,
    decimal? MinimumPurchaseAmount,
    DateTime EndsAtUtc,
    DateOnly EndsAtLocalDate,
    string Value,
    string ChangeMessage,
    string BackValue);

public sealed record WalletCustomMessage(
    Guid NotificationId,
    string Title,
    string ShortMessage,
    string LongMessage,
    DateTime DisplayUntilUtc,
    string ChangeMessage);

public sealed record WalletRecentVisibleEvent(
    Guid NotificationId,
    KBeauty.Loyalty.Domain.Enums.NotificationType Type,
    DateTime CreatedAt,
    DateTime? ProcessedAt,
    DateTime DisplayUntilUtc);
