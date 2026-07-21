using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Notifications.PointCampaign;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class PointCampaignNotificationReadService : IPointCampaignNotificationReadService
{
    private readonly AppDbContext _db;
    private readonly ILogger<PointCampaignNotificationReadService> _logger;

    public PointCampaignNotificationReadService(
        AppDbContext db,
        ILogger<PointCampaignNotificationReadService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PointCampaignNotificationPreviewDto> ListCandidatesAsync(
        string timeZoneId,
        bool includeAlreadyNotified,
        CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;
        var timeZone = PointsExpirationNotificationReadService.ResolveTimeZone(timeZoneId);

        var activeCampaigns = await _db.PointCampaigns
            .AsNoTracking()
            .Where(c => c.IsActive
                     && c.StartsAtUtc <= nowUtc
                     && c.EndsAtUtc >= nowUtc)
            .OrderByDescending(c => c.Multiplier)
            .ThenBy(c => c.MinimumPurchaseAmount ?? 0)
            .ThenByDescending(c => c.StartsAtUtc)
            .ThenBy(c => c.Id)
            .ToListAsync(ct);

        _logger.LogInformation(
            "Point campaign notification preview: activeCampaignsFound={ActiveCampaignsFound}.",
            activeCampaigns.Count);

        if (activeCampaigns.Count == 0)
        {
            return new PointCampaignNotificationPreviewDto(
                nowUtc,
                0,
                0,
                0,
                Array.Empty<PointCampaignNotificationCandidateDto>());
        }

        var cards = await (
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
                card.SerialNumber,
                card.Level
            })
            .OrderBy(x => x.CustomerName)
            .ThenBy(x => x.SerialNumber)
            .ToListAsync(ct);

        var bestByCard = cards
            .Select(card => new
            {
                Card = card,
                Campaign = SelectBestCampaign(activeCampaigns, card.Level)
            })
            .Where(x => x.Campaign is not null)
            .ToList();

        var correlations = bestByCard
            .Select(x => BuildCorrelationId(x.Campaign!.Id, x.Card.SerialNumber))
            .ToArray();
        var existingRows = correlations.Length == 0
            ? new List<string>()
            : await _db.LoyaltyNotifications
                .AsNoTracking()
                .Where(n => n.Type == NotificationType.PointCampaignStarted
                         && n.CorrelationId != null
                         && correlations.Contains(n.CorrelationId))
                .Select(n => n.CorrelationId!)
                .ToListAsync(ct);
        var existing = existingRows.ToHashSet(StringComparer.Ordinal);

        var candidates = bestByCard
            .Select(x =>
            {
                var campaign = x.Campaign!;
                var correlationId = BuildCorrelationId(campaign.Id, x.Card.SerialNumber);
                var endsAtLocal = TimeZoneInfo.ConvertTimeFromUtc(campaign.EndsAtUtc, timeZone);

                _logger.LogInformation(
                    "Point campaign candidate evaluated: serial={Serial}, level={Level}, campaign={CampaignId}, multiplier={Multiplier}, alreadyNotified={AlreadyNotified}.",
                    x.Card.SerialNumber,
                    x.Card.Level,
                    campaign.Id,
                    campaign.Multiplier,
                    existing.Contains(correlationId));

                return new PointCampaignNotificationCandidateDto(
                    x.Card.CustomerId,
                    x.Card.LoyaltyCardId,
                    x.Card.CustomerName,
                    x.Card.SerialNumber,
                    x.Card.Level,
                    campaign.Id,
                    campaign.Name,
                    campaign.Multiplier,
                    campaign.MinimumPurchaseAmount,
                    campaign.LevelEligibility,
                    campaign.StartsAtUtc,
                    campaign.EndsAtUtc,
                    endsAtLocal,
                    correlationId,
                    existing.Contains(correlationId));
            })
            .Where(x => includeAlreadyNotified || !x.AlreadyNotified)
            .ToList();

        _logger.LogInformation(
            "Point campaign notification preview result: cardsEvaluated={CardsEvaluated}, cardsEligible={CardsEligible}, candidatesReturned={CandidatesReturned}, alreadyNotified={AlreadyNotified}.",
            cards.Count,
            bestByCard.Count,
            candidates.Count,
            bestByCard.Count - candidates.Count);

        return new PointCampaignNotificationPreviewDto(
            nowUtc,
            activeCampaigns.Count,
            cards.Count,
            bestByCard.Count,
            candidates.AsReadOnly());
    }

    internal static PointCampaign? SelectBestCampaign(IEnumerable<PointCampaign> campaigns, string level) =>
        campaigns
            .Where(c => c.AppliesToLevel(level))
            .OrderByDescending(c => c.Multiplier)
            .ThenBy(c => c.MinimumPurchaseAmount ?? 0)
            .ThenByDescending(c => c.StartsAtUtc)
            .ThenBy(c => c.Id)
            .FirstOrDefault();

    internal static string BuildCorrelationId(Guid campaignId, string serialNumber) =>
        $"point-campaign-started:{campaignId:N}:{serialNumber}";
}
