using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Repositories;

internal sealed class LoyaltyNotificationRepository : ILoyaltyNotificationRepository
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public LoyaltyNotificationRepository(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public Task<LoyaltyNotification?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.LoyaltyNotifications
            .Include(n => n.Deliveries)
            .FirstOrDefaultAsync(n => n.TenantId == _tenantContext.RequireTenantId() && n.Id == id, ct);

    public Task<LoyaltyNotification?> GetByCorrelationIdAsync(string correlationId, CancellationToken ct = default) =>
        _db.LoyaltyNotifications
            .Include(n => n.Deliveries)
            .FirstOrDefaultAsync(n => n.TenantId == _tenantContext.RequireTenantId() && n.CorrelationId == correlationId, ct);

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
            .Where(n => n.TenantId == _tenantContext.RequireTenantId())
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
        var retryAttempt1Before = nowUtc.AddMinutes(-1);
        var retryAttempt2Before = nowUtc.AddMinutes(-5);
        var stuckBefore = nowUtc.AddMinutes(-15);

        var rows = await _db.LoyaltyNotifications
            .Include(n => n.Deliveries)
            .Where(n => n.TenantId == _tenantContext.RequireTenantId()
                     && (!n.ScheduledAtUtc.HasValue || n.ScheduledAtUtc <= nowUtc)
                     && (
                         (n.Status == NotificationStatus.Pending
                          && n.Deliveries.Any(d =>
                              d.Status == NotificationDeliveryStatus.Pending
                              && d.AttemptCount < maxAttempts))
                         || (n.Status == NotificationStatus.Failed
                             && n.Deliveries.Any(d =>
                                 d.Status == NotificationDeliveryStatus.Failed
                                 && d.AttemptCount < maxAttempts
                                 && d.FailureReason != null
                                 && d.FailureReason.StartsWith(NotificationDeliveryFailureReasons.TransientApnsFailurePrefix)
                                 && d.CompletedAt.HasValue
                                 && ((d.AttemptCount == 1 && d.CompletedAt.Value <= retryAttempt1Before)
                                     || (d.AttemptCount == 2 && d.CompletedAt.Value <= retryAttempt2Before)
                                     || (d.AttemptCount >= 3 && d.CompletedAt.Value <= stuckBefore))))
                         || (n.Status == NotificationStatus.Processing
                             && n.ProcessingStartedAt.HasValue
                             && n.ProcessingStartedAt.Value <= stuckBefore
                             && n.Deliveries.Any(d =>
                                 d.Status == NotificationDeliveryStatus.Processing
                                 && d.AttemptCount < maxAttempts))))
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
