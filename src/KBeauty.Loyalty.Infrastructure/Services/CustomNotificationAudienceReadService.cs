using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Application.Notifications.Custom;
using KBeauty.Loyalty.Common.Constants;
using KBeauty.Loyalty.Domain.Enums;
using KBeauty.Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KBeauty.Loyalty.Infrastructure.Services;

internal sealed class CustomNotificationAudienceReadService : ICustomNotificationAudienceReadService
{
    private readonly AppDbContext _db;
    private readonly ILogger<CustomNotificationAudienceReadService> _logger;

    public CustomNotificationAudienceReadService(
        AppDbContext db,
        ILogger<CustomNotificationAudienceReadService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CustomNotificationAudiencePreviewDto> PreviewAsync(
        CustomNotificationAudienceType audienceType,
        int? minimumPoints,
        int? pointsExpiringDaysAhead,
        int sampleSize,
        CancellationToken ct = default)
    {
        var recipients = await ResolveRecipientsAsync(audienceType, minimumPoints, pointsExpiringDaysAhead, ct);
        var excluded = await CountExcludedWithoutDeviceRegistrationAsync(audienceType, minimumPoints, pointsExpiringDaysAhead, ct);
        var distribution = recipients
            .GroupBy(r => r.Level)
            .OrderBy(g => LevelRank(g.Key))
            .Select(g => new CustomNotificationLevelDistributionDto(g.Key, g.Count()))
            .ToList();
        var warnings = new List<string>();
        if (recipients.Count == 0)
            warnings.Add("La audiencia no tiene destinatarios con Apple Wallet registrado.");

        var criteria = BuildCriteria(audienceType, minimumPoints, pointsExpiringDaysAhead);
        _logger.LogInformation(
            "Custom notification audience preview. audience={AudienceType}, recipients={Recipients}, excludedWithoutDevice={ExcludedWithoutDevice}.",
            audienceType,
            recipients.Count,
            excluded);

        return new CustomNotificationAudiencePreviewDto(
            audienceType,
            recipients.Count,
            excluded,
            distribution,
            recipients.Take(Math.Clamp(sampleSize, 1, 100)).ToList(),
            criteria,
            warnings);
    }

    public async Task<IReadOnlyList<CustomNotificationAudienceRecipientDto>> ResolveRecipientsAsync(
        CustomNotificationAudienceType audienceType,
        int? minimumPoints,
        int? pointsExpiringDaysAhead,
        CancellationToken ct = default)
    {
        var rows = audienceType == CustomNotificationAudienceType.PointsExpiring
            ? await QueryPointsExpiringRecipientsAsync(pointsExpiringDaysAhead ?? 15, ct)
            : await QueryBaseRecipientsAsync(audienceType, minimumPoints, requireDeviceRegistration: true, ct);

        return rows
            .OrderBy(r => r.CustomerName)
            .ThenBy(r => r.SerialNumber)
            .ToList()
            .AsReadOnly();
    }

    private async Task<int> CountExcludedWithoutDeviceRegistrationAsync(
        CustomNotificationAudienceType audienceType,
        int? minimumPoints,
        int? pointsExpiringDaysAhead,
        CancellationToken ct)
    {
        if (audienceType == CustomNotificationAudienceType.PointsExpiring)
        {
            var candidates = await QueryPointsExpiringCandidateSerialsAsync(pointsExpiringDaysAhead ?? 15, ct);
            return candidates.Count(c => c.DeviceRegistrationCount == 0);
        }

        var rows = await QueryBaseRecipientsAsync(audienceType, minimumPoints, requireDeviceRegistration: false, ct);
        return rows.Count(r => r.DeviceRegistrationCount == 0);
    }

    private async Task<List<CustomNotificationAudienceRecipientDto>> QueryBaseRecipientsAsync(
        CustomNotificationAudienceType audienceType,
        int? minimumPoints,
        bool requireDeviceRegistration,
        CancellationToken ct)
    {
        var query =
            from card in _db.LoyaltyCards.AsNoTracking()
            join customer in _db.Customers.AsNoTracking() on card.CustomerId equals customer.Id
            where card.IsActive && customer.IsActive
            select new
            {
                customer.Id,
                cardId = card.Id,
                customer.FullName,
                card.SerialNumber,
                card.Level,
                card.CurrentPoints,
                DeviceRegistrationCount = _db.DeviceRegistrations
                    .AsNoTracking()
                    .Count(registration => registration.SerialNumber == card.SerialNumber)
            };

        query = audienceType switch
        {
            CustomNotificationAudienceType.GlowAndAbove =>
                query.Where(x => x.Level == LoyaltyConstants.Levels.Glow || x.Level == LoyaltyConstants.Levels.Radiance),
            CustomNotificationAudienceType.RadianceOnly =>
                query.Where(x => x.Level == LoyaltyConstants.Levels.Radiance),
            CustomNotificationAudienceType.MinimumPoints =>
                query.Where(x => x.CurrentPoints >= (minimumPoints ?? 0)),
            _ => query
        };

        if (requireDeviceRegistration)
            query = query.Where(x => x.DeviceRegistrationCount > 0);

        return await query
            .OrderBy(x => x.FullName)
            .ThenBy(x => x.SerialNumber)
            .Select(x => new CustomNotificationAudienceRecipientDto(
                x.Id,
                x.cardId,
                x.FullName,
                x.SerialNumber,
                x.Level,
                x.CurrentPoints,
                x.DeviceRegistrationCount))
            .ToListAsync(ct);
    }

    private async Task<List<CustomNotificationAudienceRecipientDto>> QueryPointsExpiringRecipientsAsync(int daysAhead, CancellationToken ct)
    {
        var candidates = await QueryPointsExpiringCandidateSerialsAsync(daysAhead, ct);
        return candidates
            .Where(c => c.DeviceRegistrationCount > 0)
            .Select(c => new CustomNotificationAudienceRecipientDto(
                c.CustomerId,
                c.LoyaltyCardId,
                c.CustomerName,
                c.SerialNumber,
                c.Level,
                c.CurrentPoints,
                c.DeviceRegistrationCount))
            .ToList();
    }

    private async Task<List<PointsExpiringCandidateRow>> QueryPointsExpiringCandidateSerialsAsync(int daysAhead, CancellationToken ct)
    {
        var timeZoneId = "America/Tijuana";
        var timeZone = PointsExpirationNotificationReadService.ResolveTimeZone(timeZoneId);
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
        var targetLocalDate = DateOnly.FromDateTime(nowLocal.AddDays(daysAhead));
        var (startUtc, endUtc) = PointsExpirationNotificationReadService.GetLocalDateUtcWindow(targetLocalDate, timeZoneId);

        var rows = await (
            from lot in _db.PointLots.AsNoTracking()
            join card in _db.LoyaltyCards.AsNoTracking() on lot.LoyaltyCardId equals card.Id
            join customer in _db.Customers.AsNoTracking() on card.CustomerId equals customer.Id
            where lot.RemainingAmount > 0
               && lot.ExpiresAt >= startUtc
               && lot.ExpiresAt < endUtc
               && card.IsActive
               && customer.IsActive
            group lot by new
            {
                CustomerId = customer.Id,
                LoyaltyCardId = card.Id,
                CustomerName = customer.FullName,
                card.SerialNumber,
                card.Level,
                card.CurrentPoints
            }
            into g
            orderby g.Key.CustomerName, g.Key.SerialNumber
            select new PointsExpiringCandidateRow(
                g.Key.CustomerId,
                g.Key.LoyaltyCardId,
                g.Key.CustomerName,
                g.Key.SerialNumber,
                g.Key.Level,
                g.Key.CurrentPoints,
                _db.DeviceRegistrations
                    .AsNoTracking()
                    .Count(registration => registration.SerialNumber == g.Key.SerialNumber)))
            .ToListAsync(ct);

        return rows;
    }

    private static string BuildCriteria(
        CustomNotificationAudienceType audienceType,
        int? minimumPoints,
        int? pointsExpiringDaysAhead) =>
        audienceType switch
        {
            CustomNotificationAudienceType.AllWalletUsers => "Clientas activas con Wallet registrado.",
            CustomNotificationAudienceType.MistAndAbove => "Clientas Mist, Glow o Radiance con Wallet registrado.",
            CustomNotificationAudienceType.GlowAndAbove => "Clientas Glow o Radiance con Wallet registrado.",
            CustomNotificationAudienceType.RadianceOnly => "Clientas Radiance con Wallet registrado.",
            CustomNotificationAudienceType.MinimumPoints => $"Clientas con al menos {minimumPoints ?? 0:N0} puntos y Wallet registrado.",
            CustomNotificationAudienceType.PointsExpiring => $"Clientas con puntos que expiran en {pointsExpiringDaysAhead ?? 15:N0} dia(s) y Wallet registrado.",
            _ => audienceType.ToString()
        };

    private static int LevelRank(string level) => level switch
    {
        LoyaltyConstants.Levels.Mist => 1,
        LoyaltyConstants.Levels.Glow => 2,
        LoyaltyConstants.Levels.Radiance => 3,
        _ => 99
    };

    private sealed record PointsExpiringCandidateRow(
        Guid CustomerId,
        Guid LoyaltyCardId,
        string CustomerName,
        string SerialNumber,
        string Level,
        int CurrentPoints,
        int DeviceRegistrationCount);
}
