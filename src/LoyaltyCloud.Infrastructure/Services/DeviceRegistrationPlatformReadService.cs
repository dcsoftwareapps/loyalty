using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class DeviceRegistrationPlatformReadService : IDeviceRegistrationPlatformReadService
{
    private readonly AppDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<DeviceRegistrationPlatformReadService> _logger;

    public DeviceRegistrationPlatformReadService(
        AppDbContext db,
        IDateTimeProvider clock,
        ILogger<DeviceRegistrationPlatformReadService> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task<WalletUpdatableSerialsResult> GetUpdatableSerialsAsync(
        string deviceLibraryIdentifier,
        string passTypeIdentifier,
        DateTime? passesUpdatedSince,
        CancellationToken cancellationToken = default)
    {
        var rows = await (
            from registration in _db.DeviceRegistrations.IgnoreQueryFilters().AsNoTracking()
            join card in _db.LoyaltyCards.IgnoreQueryFilters().AsNoTracking()
                on new { registration.TenantId, registration.SerialNumber }
                equals new { card.TenantId, card.SerialNumber }
            join tenant in _db.Tenants.AsNoTracking()
                on registration.TenantId equals tenant.Id
            where registration.DeviceLibraryIdentifier == deviceLibraryIdentifier
               && registration.PassTypeIdentifier == passTypeIdentifier
               && (!passesUpdatedSince.HasValue || card.LastActivityAt > passesUpdatedSince.Value)
            select new
            {
                card.SerialNumber,
                card.LastActivityAt,
                tenant.IsActive,
                SubscriptionStatus = tenant.Subscription == null
                    ? null
                    : (LoyaltyCloud.Domain.Enums.TenantSubscriptionStatus?)tenant.Subscription.Status,
                CurrentPeriodEnd = tenant.Subscription == null
                    ? null
                    : tenant.Subscription.CurrentPeriodEnd,
                PaidThroughUtc = tenant.Subscription == null
                    ? null
                    : tenant.Subscription.PaidThroughUtc,
                GracePeriodEndsAt = tenant.Subscription == null
                    ? null
                    : tenant.Subscription.GracePeriodEndsAt
            })
            .ToListAsync(cancellationToken);

        var now = _clock.UtcNow;
        var serials = rows
            .Where(row => row.IsActive
                       && row.SubscriptionStatus.HasValue
                       && TenantSubscription.IsOperational(
                           row.SubscriptionStatus.Value,
                           row.CurrentPeriodEnd,
                           row.PaidThroughUtc,
                           row.GracePeriodEndsAt,
                           now))
            .OrderBy(row => row.LastActivityAt)
            .Select(row => row.SerialNumber)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        _logger.LogInformation(
            "Apple Wallet platform registration lookup: device={Device}, passType={PassType}, since={Since}, serials=[{Serials}].",
            SafeDeviceIdentifier(deviceLibraryIdentifier),
            passTypeIdentifier,
            passesUpdatedSince.HasValue ? passesUpdatedSince.Value.ToString("O") : "beginning",
            string.Join(", ", serials));

        return new WalletUpdatableSerialsResult(serials, now);
    }

    private static string SafeDeviceIdentifier(string value) =>
        value.Length <= 8 ? value : $"{value[..4]}...{value[^4..]}";
}
