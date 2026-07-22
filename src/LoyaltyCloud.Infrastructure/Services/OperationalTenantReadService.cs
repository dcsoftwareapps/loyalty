using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class OperationalTenantReadService : IOperationalTenantReadService
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _clock;

    public OperationalTenantReadService(AppDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<TenantExecutionInfo>> ListTenantsForExecutionAsync(CancellationToken ct = default)
    {
        var rows = await _db.Tenants
            .AsNoTracking()
            .OrderBy(t => t.Slug)
            .Select(t => new
            {
                t.Id,
                t.Slug,
                t.TimeZoneId,
                t.IsActive,
                SubscriptionStatus = t.Subscription == null
                    ? (TenantSubscriptionStatus?)null
                    : t.Subscription.Status,
                CurrentPeriodEnd = t.Subscription == null
                    ? null
                    : t.Subscription.CurrentPeriodEnd,
                PaidThroughUtc = t.Subscription == null
                    ? null
                    : t.Subscription.PaidThroughUtc,
                GracePeriodEndsAt = t.Subscription == null
                    ? null
                    : t.Subscription.GracePeriodEndsAt
            })
            .ToListAsync(ct);

        return rows
            .Select(row =>
            {
                var isSubscriptionOperational = row.SubscriptionStatus.HasValue
                    && TenantSubscription.IsOperational(
                        row.SubscriptionStatus.Value,
                        row.CurrentPeriodEnd,
                        row.PaidThroughUtc,
                        row.GracePeriodEndsAt,
                        _clock.UtcNow);
                var isOperational = row.IsActive && isSubscriptionOperational;
                var skipReason = GetSkipReason(row.IsActive, row.SubscriptionStatus, row.GracePeriodEndsAt);
                return new TenantExecutionInfo(
                    row.Id,
                    row.Slug,
                    row.TimeZoneId,
                    row.SubscriptionStatus ?? TenantSubscriptionStatus.Cancelled,
                    row.GracePeriodEndsAt,
                    isOperational,
                    isOperational ? null : skipReason);
            })
            .ToList()
            .AsReadOnly();
    }

    private string GetSkipReason(
        bool isActive,
        TenantSubscriptionStatus? subscriptionStatus,
        DateTime? gracePeriodEndsAt)
    {
        if (!isActive)
            return "Tenant inactive.";

        if (!subscriptionStatus.HasValue)
            return "Tenant subscription missing.";

        if (subscriptionStatus is TenantSubscriptionStatus.Suspended or TenantSubscriptionStatus.Cancelled)
            return $"Tenant subscription is {subscriptionStatus}.";

        if (subscriptionStatus == TenantSubscriptionStatus.PastDue
            && gracePeriodEndsAt.HasValue
            && gracePeriodEndsAt.Value < _clock.UtcNow)
        {
            return "Tenant subscription is PastDue with expired grace period.";
        }

        return $"Tenant subscription is not operational: {subscriptionStatus}.";
    }
}
