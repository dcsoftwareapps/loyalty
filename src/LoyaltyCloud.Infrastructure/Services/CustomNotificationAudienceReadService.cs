using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Notifications.Custom;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class CustomNotificationAudienceReadService : ICustomNotificationAudienceReadService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantLoyaltyLevelReadService _tenantLevels;
    private readonly ILogger<CustomNotificationAudienceReadService> _logger;

    public CustomNotificationAudienceReadService(
        AppDbContext db,
        ITenantContext tenantContext,
        ITenantLoyaltyLevelReadService tenantLevels,
        ILogger<CustomNotificationAudienceReadService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _tenantLevels = tenantLevels;
        _logger = logger;
    }

    public async Task<CustomNotificationAudiencePreviewDto> PreviewAsync(
        string audienceType,
        int? minimumPoints,
        int? pointsExpiringDaysAhead,
        int sampleSize,
        CancellationToken ct = default)
    {
        var recipients = await ResolveRecipientsAsync(audienceType, minimumPoints, pointsExpiringDaysAhead, ct);
        var excluded = await CountExcludedWithoutDeviceRegistrationAsync(audienceType, minimumPoints, pointsExpiringDaysAhead, ct);
        var levelRanks = await BuildLevelRanksAsync(ct);
        var distribution = recipients
            .GroupBy(r => r.Level)
            .OrderBy(g => levelRanks.TryGetValue(g.Key, out var rank) ? rank : int.MaxValue)
            .Select(g => new CustomNotificationLevelDistributionDto(g.Key, g.Count()))
            .ToList();
        var warnings = new List<string>();
        if (recipients.Count == 0)
            warnings.Add("La audiencia no tiene destinatarios con Wallet registrado.");

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
            warnings,
            recipients.Count(r => r.DeviceRegistrationCount > 0),
            recipients.Count(r => r.GoogleWalletCount > 0));
    }

    public async Task<IReadOnlyList<CustomNotificationAudienceRecipientDto>> ResolveRecipientsAsync(
        string audienceType,
        int? minimumPoints,
        int? pointsExpiringDaysAhead,
        CancellationToken ct = default)
    {
        var rows = CustomNotificationCampaign.IsPointsExpiringAudience(audienceType)
            ? await QueryPointsExpiringRecipientsAsync(pointsExpiringDaysAhead ?? 15, ct)
            : await QueryBaseRecipientsAsync(audienceType, minimumPoints, requireDeviceRegistration: true, ct);

        return rows
            .OrderBy(r => r.CustomerName)
            .ThenBy(r => r.SerialNumber)
            .ToList()
            .AsReadOnly();
    }

    private async Task<int> CountExcludedWithoutDeviceRegistrationAsync(
        string audienceType,
        int? minimumPoints,
        int? pointsExpiringDaysAhead,
        CancellationToken ct)
    {
        if (CustomNotificationCampaign.IsPointsExpiringAudience(audienceType))
        {
            var candidates = await QueryPointsExpiringCandidateSerialsAsync(pointsExpiringDaysAhead ?? 15, ct);
            return candidates.Count(c => c.DeviceRegistrationCount == 0 && c.GoogleWalletCount == 0);
        }

        var rows = await QueryBaseRecipientsAsync(audienceType, minimumPoints, requireDeviceRegistration: false, ct);
        return rows.Count(r => r.DeviceRegistrationCount == 0 && r.GoogleWalletCount == 0);
    }

    private async Task<List<CustomNotificationAudienceRecipientDto>> QueryBaseRecipientsAsync(
        string audienceType,
        int? minimumPoints,
        bool requireDeviceRegistration,
        CancellationToken ct)
    {
        var tenantId = _tenantContext.RequireTenantId();
        var eligibleLevels = await ResolveEligibleLevelNamesAsync(audienceType, ct);
        var query =
            from card in _db.LoyaltyCards.AsNoTracking()
            join customer in _db.Customers.AsNoTracking() on card.CustomerId equals customer.Id
            where card.TenantId == tenantId
               && customer.TenantId == tenantId
               && card.IsActive
               && customer.IsActive
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
                    .Count(registration => registration.TenantId == tenantId && registration.SerialNumber == card.SerialNumber),
                GoogleWalletCount = _db.MemberDigitalWallets
                    .AsNoTracking()
                    .Count(wallet => wallet.TenantId == tenantId
                        && wallet.LoyaltyCardId == card.Id
                        && wallet.Provider == DigitalWalletProvider.Google
                        && wallet.Status == DigitalWalletStatus.Active
                        && wallet.ExternalObjectId != string.Empty
                        && wallet.ExternalClassId != string.Empty)
            };

        if (eligibleLevels is not null)
            query = query.Where(x => eligibleLevels.Contains(x.Level));

        if (CustomNotificationCampaign.IsMinimumPointsAudience(audienceType))
            query = query.Where(x => x.CurrentPoints >= (minimumPoints ?? 0));

        if (requireDeviceRegistration)
            query = query.Where(x => x.DeviceRegistrationCount > 0 || x.GoogleWalletCount > 0);

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
                x.DeviceRegistrationCount,
                x.GoogleWalletCount))
            .ToListAsync(ct);
    }

    private async Task<List<CustomNotificationAudienceRecipientDto>> QueryPointsExpiringRecipientsAsync(int daysAhead, CancellationToken ct)
    {
        var candidates = await QueryPointsExpiringCandidateSerialsAsync(daysAhead, ct);
        return candidates
            .Where(c => c.DeviceRegistrationCount > 0 || c.GoogleWalletCount > 0)
            .Select(c => new CustomNotificationAudienceRecipientDto(
                c.CustomerId,
                c.LoyaltyCardId,
                c.CustomerName,
                c.SerialNumber,
                c.Level,
                c.CurrentPoints,
                c.DeviceRegistrationCount,
                c.GoogleWalletCount))
            .ToList();
    }

    private async Task<List<PointsExpiringCandidateRow>> QueryPointsExpiringCandidateSerialsAsync(int daysAhead, CancellationToken ct)
    {
        var timeZoneId = "America/Tijuana";
        var tenantId = _tenantContext.RequireTenantId();
        var timeZone = PointsExpirationNotificationReadService.ResolveTimeZone(timeZoneId);
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
        var targetLocalDate = DateOnly.FromDateTime(nowLocal.AddDays(daysAhead));
        var (startUtc, endUtc) = PointsExpirationNotificationReadService.GetLocalDateUtcWindow(targetLocalDate, timeZoneId);

        var rows = await (
            from lot in _db.PointLots.AsNoTracking()
            join card in _db.LoyaltyCards.AsNoTracking() on lot.LoyaltyCardId equals card.Id
            join customer in _db.Customers.AsNoTracking() on card.CustomerId equals customer.Id
            where lot.TenantId == tenantId
               && lot.RemainingAmount > 0
               && lot.ExpiresAt >= startUtc
               && lot.ExpiresAt < endUtc
               && card.TenantId == tenantId
               && customer.TenantId == tenantId
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
                    .Count(registration => registration.TenantId == tenantId && registration.SerialNumber == g.Key.SerialNumber),
                _db.MemberDigitalWallets
                    .AsNoTracking()
                    .Count(wallet => wallet.TenantId == tenantId
                        && wallet.LoyaltyCardId == g.Key.LoyaltyCardId
                        && wallet.Provider == DigitalWalletProvider.Google
                        && wallet.Status == DigitalWalletStatus.Active
                        && wallet.ExternalObjectId != string.Empty
                        && wallet.ExternalClassId != string.Empty)))
            .ToListAsync(ct);

        return rows;
    }

    private static string BuildCriteria(
        string audienceType,
        int? minimumPoints,
        int? pointsExpiringDaysAhead)
    {
        if (CustomNotificationCampaign.IsAllWalletUsersAudience(audienceType))
            return "Clientes activos con Wallet registrado.";
        if (CustomNotificationCampaign.IsMinimumPointsAudience(audienceType))
            return $"Clientes con al menos {minimumPoints ?? 0:N0} puntos y Wallet registrado.";
        if (CustomNotificationCampaign.IsPointsExpiringAudience(audienceType))
            return $"Clientes con puntos que expiran en {pointsExpiringDaysAhead ?? 15:N0} dia(s) y Wallet registrado.";

        return $"Clientes desde nivel {audienceType} con Wallet registrado.";
    }

    private async Task<string[]?> ResolveEligibleLevelNamesAsync(string audienceType, CancellationToken ct)
    {
        if (CustomNotificationCampaign.IsAllWalletUsersAudience(audienceType) ||
            CustomNotificationCampaign.IsMinimumPointsAudience(audienceType))
            return null;

        var levels = await _tenantLevels.GetActiveLevelsAsync(ct);
        var levelRanks = BuildLevelRanks(levels);
        if (TryResolveLegacyAudience(audienceType, levels, out var legacyLevels))
            return legacyLevels;

        if (!levelRanks.TryGetValue(audienceType, out var requiredRank))
            return [];

        return levelRanks
            .Where(level => level.Value >= requiredRank)
            .Select(level => level.Key)
            .ToArray();
    }

    private async Task<IReadOnlyDictionary<string, int>> BuildLevelRanksAsync(CancellationToken ct)
    {
        var levels = await _tenantLevels.GetActiveLevelsAsync(ct);
        return BuildLevelRanks(levels);
    }

    private static IReadOnlyDictionary<string, int> BuildLevelRanks(IReadOnlyList<TenantLoyaltyLevelDto> levels) =>
        levels.ToDictionary(
            level => level.Name,
            level => level.SortOrder,
            StringComparer.OrdinalIgnoreCase);

    private static bool TryResolveLegacyAudience(
        string audienceType,
        IReadOnlyList<TenantLoyaltyLevelDto> levels,
        out string[] eligibleLevels)
    {
        var orderedLevels = levels.OrderBy(level => level.SortOrder).ToArray();
        if (string.Equals(audienceType, nameof(CustomNotificationAudienceType.MistAndAbove), StringComparison.OrdinalIgnoreCase))
        {
            eligibleLevels = orderedLevels.Select(level => level.Name).ToArray();
            return true;
        }

        if (string.Equals(audienceType, nameof(CustomNotificationAudienceType.GlowAndAbove), StringComparison.OrdinalIgnoreCase))
        {
            eligibleLevels = orderedLevels.Skip(1).Select(level => level.Name).ToArray();
            return true;
        }

        if (string.Equals(audienceType, nameof(CustomNotificationAudienceType.RadianceOnly), StringComparison.OrdinalIgnoreCase))
        {
            eligibleLevels = orderedLevels.Skip(2).Take(1).Select(level => level.Name).ToArray();
            return true;
        }

        eligibleLevels = [];
        return false;
    }

    private sealed record PointsExpiringCandidateRow(
        Guid CustomerId,
        Guid LoyaltyCardId,
        string CustomerName,
        string SerialNumber,
        string Level,
        int CurrentPoints,
        int DeviceRegistrationCount,
        int GoogleWalletCount);
}
