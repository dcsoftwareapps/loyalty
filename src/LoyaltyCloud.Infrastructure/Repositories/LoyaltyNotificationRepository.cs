using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Repositories;

internal sealed class LoyaltyNotificationRepository : ILoyaltyNotificationRepository
{
    private readonly AppDbContext _db;

    public LoyaltyNotificationRepository(AppDbContext db) => _db = db;

    public Task<LoyaltyNotification?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.LoyaltyNotifications
            .Include(n => n.Deliveries)
            .FirstOrDefaultAsync(n => n.Id == id, ct);

    public Task<LoyaltyNotification?> GetByCorrelationIdAsync(string correlationId, CancellationToken ct = default) =>
        _db.LoyaltyNotifications
            .Include(n => n.Deliveries)
            .FirstOrDefaultAsync(n => n.CorrelationId == correlationId, ct);

    public async Task<IReadOnlyList<LoyaltyNotification>> ListAsync(
        Guid? customerId,
        NotificationType? type,
        NotificationStatus? status,
        NotificationChannel? channel,
        DateTime? fromUtc,
        DateTime? toUtc,
        int take,
        CancellationToken ct = default)
    {
        var query = _db.LoyaltyNotifications
            .AsNoTracking()
            .Include(n => n.Deliveries)
            .AsQueryable();

        if (customerId.HasValue)
            query = query.Where(n => n.CustomerId == customerId.Value);
        if (type.HasValue)
            query = query.Where(n => n.Type == type.Value);
        if (status.HasValue)
            query = query.Where(n => n.Status == status.Value);
        if (channel.HasValue)
            query = query.Where(n => n.Deliveries.Any(d => d.Channel == channel.Value));
        if (fromUtc.HasValue)
            query = query.Where(n => n.CreatedAt >= fromUtc.Value);
        if (toUtc.HasValue)
            query = query.Where(n => n.CreatedAt <= toUtc.Value);

        var rows = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(ct);

        return rows.AsReadOnly();
    }

    public async Task<IReadOnlyList<LoyaltyNotification>> GetPendingDueAsync(DateTime nowUtc, int take, int maxAttempts, CancellationToken ct = default)
    {
        var rows = await _db.LoyaltyNotifications
            .Include(n => n.Deliveries)
            .Where(n => n.Status == NotificationStatus.Pending
                     && (!n.ScheduledAtUtc.HasValue || n.ScheduledAtUtc <= nowUtc)
                     && n.Deliveries.Any(d => d.Status == NotificationDeliveryStatus.Pending && d.AttemptCount < maxAttempts))
            .OrderBy(n => n.ScheduledAtUtc ?? n.CreatedAt)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(ct);

        return rows.AsReadOnly();
    }

    public async Task AddAsync(LoyaltyNotification notification, CancellationToken ct = default) =>
        await _db.LoyaltyNotifications.AddAsync(notification, ct);

    public void Update(LoyaltyNotification notification)
    {
        if (_db.Entry(notification).State == EntityState.Detached)
            _db.LoyaltyNotifications.Update(notification);
    }
}
