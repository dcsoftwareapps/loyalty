using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Domain.Repositories;

public interface ILoyaltyNotificationRepository
{
    Task<LoyaltyNotification?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<LoyaltyNotification?> GetByCorrelationIdAsync(string correlationId, CancellationToken ct = default);
    Task<IReadOnlyList<LoyaltyNotification>> ListAsync(
        Guid? customerId,
        NotificationType? type,
        NotificationStatus? status,
        NotificationChannel? channel,
        DateTime? fromUtc,
        DateTime? toUtc,
        int take,
        CancellationToken ct = default);
    Task<IReadOnlyList<LoyaltyNotification>> GetPendingDueAsync(DateTime nowUtc, int take, int maxAttempts, CancellationToken ct = default);
    Task AddAsync(LoyaltyNotification notification, CancellationToken ct = default);
    void Update(LoyaltyNotification notification);
}
