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
    WalletMonthlyProductMessage? MonthlyProduct);

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
