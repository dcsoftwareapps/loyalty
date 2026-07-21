using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Notifications.MonthlyProduct;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class MonthlyProductNotificationReadService : IMonthlyProductNotificationReadService
{
    private readonly AppDbContext _db;
    private readonly ILogger<MonthlyProductNotificationReadService> _logger;

    public MonthlyProductNotificationReadService(
        AppDbContext db,
        ILogger<MonthlyProductNotificationReadService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<MonthlyProductNotificationPreviewDto> ListCandidatesAsync(
        string timeZoneId,
        bool includeAlreadyNotified,
        CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;
        var timeZone = PointsExpirationNotificationReadService.ResolveTimeZone(timeZoneId);
        var product = await _db.RewardCatalogItems
            .AsNoTracking()
            .Where(r => r.IsMonthlyProduct
                     && r.IsActive
                     && r.ValidFrom.HasValue
                     && r.ValidTo.HasValue
                     && r.ValidFrom.Value <= nowUtc
                     && r.ValidTo.Value >= nowUtc)
            .OrderBy(r => r.ValidFrom)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.PointsCost,
                ValidFromUtc = r.ValidFrom!.Value,
                ValidToUtc = r.ValidTo!.Value
            })
            .FirstOrDefaultAsync(ct);

        if (product is null)
        {
            _logger.LogInformation("Monthly product notification preview skipped: no active monthly product at {NowUtc}.", nowUtc);
            return new MonthlyProductNotificationPreviewDto(
                nowUtc,
                null,
                null,
                null,
                null,
                null,
                null,
                0,
                Array.Empty<MonthlyProductNotificationCandidateDto>());
        }

        var validToLocalDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(product.ValidToUtc, timeZone).Date);

        var eligibleCards = await (
            from card in _db.LoyaltyCards.AsNoTracking()
            join customer in _db.Customers.AsNoTracking() on card.CustomerId equals customer.Id
            where card.IsActive
               && customer.IsActive
               && _db.DeviceRegistrations.AsNoTracking().Any(d => d.SerialNumber == card.SerialNumber)
            select new
            {
                CustomerId = customer.Id,
                LoyaltyCardId = card.Id,
                CustomerName = customer.FullName,
                card.SerialNumber
            })
            .OrderBy(x => x.CustomerName)
            .ThenBy(x => x.SerialNumber)
            .ToListAsync(ct);

        var correlations = eligibleCards
            .Select(x => BuildCorrelationId(product.Id, x.SerialNumber))
            .ToArray();
        var existingRows = correlations.Length == 0
            ? new List<string>()
            : await _db.LoyaltyNotifications
                .AsNoTracking()
                .Where(n => n.Type == NotificationType.MonthlyProductStarted
                         && n.CorrelationId != null
                         && correlations.Contains(n.CorrelationId))
                .Select(n => n.CorrelationId!)
                .ToListAsync(ct);
        var existing = existingRows.ToHashSet(StringComparer.Ordinal);

        var candidates = eligibleCards
            .Select(x =>
            {
                var correlationId = BuildCorrelationId(product.Id, x.SerialNumber);
                return new MonthlyProductNotificationCandidateDto(
                    x.CustomerId,
                    x.LoyaltyCardId,
                    x.CustomerName,
                    x.SerialNumber,
                    product.Id,
                    product.Name,
                    product.PointsCost,
                    product.ValidToUtc,
                    validToLocalDate,
                    correlationId,
                    existing.Contains(correlationId));
            })
            .Where(x => includeAlreadyNotified || !x.AlreadyNotified)
            .ToList();

        _logger.LogInformation(
            "Monthly product notification preview: reward={RewardId}, product={ProductName}, eligibleCards={EligibleCards}, candidatesReturned={CandidatesReturned}, alreadyNotified={AlreadyNotified}.",
            product.Id,
            product.Name,
            eligibleCards.Count,
            candidates.Count,
            eligibleCards.Count - candidates.Count);

        return new MonthlyProductNotificationPreviewDto(
            nowUtc,
            product.Id,
            product.Name,
            product.PointsCost,
            product.ValidFromUtc,
            product.ValidToUtc,
            validToLocalDate,
            eligibleCards.Count,
            candidates.AsReadOnly());
    }

    internal static string BuildCorrelationId(Guid rewardId, string serialNumber) =>
        $"monthly-product-started:{rewardId:N}:{serialNumber}";
}
