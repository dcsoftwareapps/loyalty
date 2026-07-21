using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class LoyaltyCardTenantLookup : ILoyaltyCardTenantLookup
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _clock;

    public LoyaltyCardTenantLookup(AppDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<WalletTenantInfo?> ResolveBySerialNumberAsync(
        string serialNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
            return null;

        var normalized = serialNumber.Trim().ToUpperInvariant();

        var row = await _db.LoyaltyCards
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(card => card.SerialNumber == normalized)
            .Join(
                _db.Tenants.AsNoTracking(),
                card => card.TenantId,
                tenant => tenant.Id,
                (card, tenant) => new
                {
                    tenant.Id,
                    tenant.Slug,
                    tenant.DisplayName,
                    tenant.TimeZoneId,
                    tenant.IsActive,
                    SubscriptionStatus = tenant.Subscription == null
                        ? null
                        : (LoyaltyCloud.Domain.Enums.TenantSubscriptionStatus?)tenant.Subscription.Status,
                    GracePeriodEndsAt = tenant.Subscription == null
                        ? null
                        : tenant.Subscription.GracePeriodEndsAt
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return null;

        var isOperational = row.IsActive
            && row.SubscriptionStatus.HasValue
            && TenantSubscription.IsOperational(
                row.SubscriptionStatus.Value,
                row.GracePeriodEndsAt,
                _clock.UtcNow);

        return new WalletTenantInfo(
            row.Id,
            row.Slug,
            row.DisplayName,
            row.TimeZoneId,
            row.IsActive,
            row.SubscriptionStatus,
            row.GracePeriodEndsAt,
            isOperational);
    }
}
