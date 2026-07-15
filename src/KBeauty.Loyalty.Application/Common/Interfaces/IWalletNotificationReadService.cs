namespace KBeauty.Loyalty.Application.Common.Interfaces;

public interface IWalletNotificationReadService
{
    Task<WalletNotificationMessage?> GetActiveMessageAsync(Guid loyaltyCardId, CancellationToken ct = default);
}

public sealed record WalletNotificationMessage(
    Guid Id,
    KBeauty.Loyalty.Domain.Enums.NotificationType Type,
    string Title,
    string Message,
    string? MetadataJson);
