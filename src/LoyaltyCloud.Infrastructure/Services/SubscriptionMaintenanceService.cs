using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class SubscriptionMaintenanceService : ISubscriptionMaintenanceService
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly BillingOptions _options;
    private readonly ILogger<SubscriptionMaintenanceService> _logger;

    public SubscriptionMaintenanceService(
        AppDbContext db,
        IDateTimeProvider clock,
        IOptions<BillingOptions> options,
        ILogger<SubscriptionMaintenanceService> logger)
    {
        _db = db;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SubscriptionMaintenanceResult> ProcessAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Subscription maintenance started.");

        var now = _clock.UtcNow;
        var gracePeriodDays = _options.ValidatedGracePeriodDays;
        var rows = await _db.Tenants
            .Include(t => t.Subscription)
            .Where(t => t.Subscription != null
                && (t.Subscription!.Status == TenantSubscriptionStatus.Trial
                    || t.Subscription.Status == TenantSubscriptionStatus.Active
                    || t.Subscription.Status == TenantSubscriptionStatus.PastDue))
            .OrderBy(t => t.Slug)
            .ToListAsync(cancellationToken);

        var trialsSuspended = 0;
        var activeMovedToPastDue = 0;
        var pastDueSuspended = 0;
        var failedTenants = 0;

        foreach (var tenant in rows)
        {
            try
            {
                var subscription = tenant.Subscription!;
                var previousStatus = subscription.Status;
                var changed = subscription.ExpireIfNeeded(now, gracePeriodDays);
                if (!changed)
                    continue;

                await _db.SaveChangesAsync(cancellationToken);

                if (previousStatus == TenantSubscriptionStatus.Trial
                    && subscription.Status == TenantSubscriptionStatus.Suspended)
                {
                    trialsSuspended++;
                    _logger.LogInformation(
                        "Trial subscription expired. TenantId={TenantId}, TenantSlug={TenantSlug}.",
                        tenant.Id,
                        tenant.Slug);
                }
                else if (previousStatus == TenantSubscriptionStatus.Active
                         && subscription.Status == TenantSubscriptionStatus.PastDue)
                {
                    activeMovedToPastDue++;
                    _logger.LogInformation(
                        "Subscription payment period expired. TenantId={TenantId}, TenantSlug={TenantSlug}, GracePeriodEndsAt={GracePeriodEndsAt}.",
                        tenant.Id,
                        tenant.Slug,
                        subscription.GracePeriodEndsAt);
                }
                else if (previousStatus == TenantSubscriptionStatus.PastDue
                         && subscription.Status == TenantSubscriptionStatus.Suspended)
                {
                    pastDueSuspended++;
                    _logger.LogInformation(
                        "Subscription grace expired. TenantId={TenantId}, TenantSlug={TenantSlug}.",
                        tenant.Id,
                        tenant.Slug);
                }
            }
            catch (Exception ex)
            {
                failedTenants++;
                _logger.LogError(
                    ex,
                    "Subscription maintenance failed for tenant. TenantId={TenantId}.",
                    tenant.Id);
            }
        }

        _logger.LogInformation(
            "Subscription maintenance completed. TenantsProcessed={TenantsProcessed}, TrialsSuspended={TrialsSuspended}, ActiveMovedToPastDue={ActiveMovedToPastDue}, PastDueSuspended={PastDueSuspended}, FailedTenants={FailedTenants}.",
            rows.Count,
            trialsSuspended,
            activeMovedToPastDue,
            pastDueSuspended,
            failedTenants);

        return new SubscriptionMaintenanceResult(
            rows.Count,
            trialsSuspended,
            activeMovedToPastDue,
            pastDueSuspended,
            failedTenants);
    }
}
