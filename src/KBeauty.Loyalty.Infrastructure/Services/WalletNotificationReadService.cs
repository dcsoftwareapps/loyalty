using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Domain.Enums;
using KBeauty.Loyalty.Domain.ValueObjects;
using KBeauty.Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;

namespace KBeauty.Loyalty.Infrastructure.Services;

internal sealed class WalletNotificationReadService : IWalletNotificationReadService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WalletNotificationReadService> _logger;
    private static readonly CultureInfo SpanishMexico = CultureInfo.GetCultureInfo("es-MX");

    public WalletNotificationReadService(
        AppDbContext db,
        IConfiguration configuration,
        ILogger<WalletNotificationReadService> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<WalletNotificationMessage?> GetActiveMessageAsync(Guid loyaltyCardId, CancellationToken ct = default)
    {
        var context = await GetActiveContextAsync(loyaltyCardId, ct);
        return context.News;
    }

    public async Task<WalletNotificationContext> GetActiveContextAsync(Guid loyaltyCardId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var rows = await _db.LoyaltyNotifications
            .AsNoTracking()
            .Where(n => n.LoyaltyCardId == loyaltyCardId
                     && (n.Status == NotificationStatus.Delivered ||
                         n.Status == NotificationStatus.PartiallyDelivered)
                     && n.DisplayUntilUtc.HasValue
                     && n.DisplayUntilUtc > now)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new
            {
                n.Id,
                n.Type,
                n.Title,
                n.Message,
                n.MetadataJson,
                n.CreatedAt,
                n.ProcessedAt,
                DisplayUntilUtc = n.DisplayUntilUtc!.Value
            })
            .Take(10)
            .ToListAsync(ct);

        var news = rows.FirstOrDefault();
        var levelChange = rows.FirstOrDefault(n => n.Type == NotificationType.LevelChanged);
        var pointsExpiring = rows.FirstOrDefault(n => n.Type == NotificationType.PointsExpiring);
        var birthdayBenefit = rows.FirstOrDefault(n => n.Type == NotificationType.BirthdayBenefitStarted);
        var pointCampaignRows = rows.Where(n => n.Type == NotificationType.PointCampaignStarted).ToList();
        var monthlyProduct = await BuildMonthlyProductMessageAsync(ct);
        var pointsExpiringMessage = pointsExpiring is null
            ? null
            : await BuildPointsExpiringMessageAsync(loyaltyCardId, pointsExpiring.Id, pointsExpiring.Message, pointsExpiring.MetadataJson, ct);
        var birthdayBenefitMessage = birthdayBenefit is null
            ? null
            : await BuildBirthdayBenefitMessageAsync(loyaltyCardId, birthdayBenefit.MetadataJson, ct);
        var pointCampaignMessage = pointCampaignRows.Count == 0
            ? null
            : await BuildPointCampaignMessageAsync(
                loyaltyCardId,
                pointCampaignRows.Select(r => new WalletNotificationMessage(r.Id, r.Type, r.Title, r.Message, r.MetadataJson)),
                ct);

        var recentVisibleEvent = SelectRecentVisibleEvent(
            rows.Select(r => new WalletRecentVisibleEvent(r.Id, r.Type, r.CreatedAt, r.ProcessedAt, r.DisplayUntilUtc)),
            pointsExpiringMessage,
            monthlyProduct,
            birthdayBenefitMessage,
            pointCampaignMessage,
            levelChange is not null,
            now);

        var context = new WalletNotificationContext(
            news is null
                ? null
                : new WalletNotificationMessage(news.Id, news.Type, news.Title, news.Message, news.MetadataJson),
            levelChange is null
                ? null
                : new WalletNotificationMessage(levelChange.Id, levelChange.Type, levelChange.Title, levelChange.Message, levelChange.MetadataJson),
            pointsExpiringMessage,
            monthlyProduct,
            birthdayBenefitMessage,
            pointCampaignMessage,
            recentVisibleEvent);

        _logger.LogInformation(
            "WalletNotificationContext for card {CardId}: activeNotifications={NotificationCount}, recentVisibleEvent={RecentVisibleEvent}, levelChange={LevelChange}, pointsExpiring={PointsExpiring}, birthdayBenefit={BirthdayBenefit}, pointCampaign={PointCampaign}, monthlyProduct={MonthlyProduct}.",
            loyaltyCardId,
            rows.Count,
            recentVisibleEvent is null
                ? "null"
                : $"{recentVisibleEvent.Type} id={recentVisibleEvent.NotificationId} processedAt={recentVisibleEvent.ProcessedAt:O} createdAt={recentVisibleEvent.CreatedAt:O}",
            context.LevelChange is null ? "null" : $"{context.LevelChange.Type} id={context.LevelChange.Id}",
            context.PointsExpiring is null ? "null" : $"{context.PointsExpiring.Value} expires={context.PointsExpiring.ExpirationDate}",
            context.BirthdayBenefit is null ? "null" : $"{context.BirthdayBenefit.Value} year={context.BirthdayBenefit.BenefitYear}",
            context.PointCampaign is null ? "null" : $"{context.PointCampaign.Value} campaign={context.PointCampaign.CampaignId}",
            context.MonthlyProduct is null ? "null" : $"{context.MonthlyProduct.Value} reward={context.MonthlyProduct.RewardId}");

        if (recentVisibleEvent is not null)
        {
            _logger.LogInformation(
                "Recent visible Wallet event selected for card {CardId}: notification={NotificationId}, type={Type}, processedAt={ProcessedAt}.",
                loyaltyCardId,
                recentVisibleEvent.NotificationId,
                recentVisibleEvent.Type,
                recentVisibleEvent.ProcessedAt);
        }
        else
        {
            _logger.LogDebug("No recent visible Wallet event selected for card {CardId}.", loyaltyCardId);
        }

        return context;
    }

    private WalletRecentVisibleEvent? SelectRecentVisibleEvent(
        IEnumerable<WalletRecentVisibleEvent> events,
        WalletPointsExpiringMessage? pointsExpiring,
        WalletMonthlyProductMessage? monthlyProduct,
        WalletBirthdayBenefitMessage? birthdayBenefit,
        WalletPointCampaignMessage? pointCampaign,
        bool hasLevelChange,
        DateTime nowUtc)
    {
        var priorityHours = GetVisibleEventPriorityHours();
        var threshold = nowUtc.AddHours(-priorityHours);

        var recent = events
            .Where(e => e.ProcessedAt.GetValueOrDefault(e.CreatedAt) >= threshold)
            .Where(e => IsSupportedAndAvailable(e, hasLevelChange, pointsExpiring, birthdayBenefit, pointCampaign, monthlyProduct))
            .OrderByDescending(e => e.ProcessedAt ?? e.CreatedAt)
            .ThenBy(e => RecentEventPriority(e.Type))
            .ThenByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.NotificationId)
            .FirstOrDefault();

        foreach (var candidate in events)
        {
            var effectiveAt = candidate.ProcessedAt.GetValueOrDefault(candidate.CreatedAt);
            var inWindow = effectiveAt >= threshold;
            var supported = IsSupportedAndAvailable(candidate, hasLevelChange, pointsExpiring, birthdayBenefit, pointCampaign, monthlyProduct);
            _logger.LogInformation(
                "Wallet recent event candidate: id={NotificationId}, type={Type}, createdAt={CreatedAt}, processedAt={ProcessedAt}, displayUntilUtc={DisplayUntilUtc}, threshold={Threshold}, inWindow={InWindow}, supportedAndAvailable={SupportedAndAvailable}.",
                candidate.NotificationId,
                candidate.Type,
                candidate.CreatedAt,
                candidate.ProcessedAt,
                candidate.DisplayUntilUtc,
                threshold,
                inWindow,
                supported);
        }

        if (recent is null)
        {
            var expiredCount = events.Count(e => e.ProcessedAt.GetValueOrDefault(e.CreatedAt) < threshold);
            if (expiredCount > 0)
            {
                _logger.LogDebug(
                    "Wallet recent event priority window expired for {ExpiredCount} notification(s). WindowHours={PriorityHours}.",
                    expiredCount,
                    priorityHours);
            }
        }

        return recent;
    }

    private int GetVisibleEventPriorityHours()
    {
        var raw = _configuration["LoyaltyNotifications:VisibleEventPriorityHours"];
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours) && hours > 0)
            return hours;

        if (!string.IsNullOrWhiteSpace(raw))
        {
            _logger.LogWarning(
                "Invalid LoyaltyNotifications:VisibleEventPriorityHours value '{Value}'. Falling back to 24 hours.",
                raw);
        }

        return 24;
    }

    private static bool IsSupportedAndAvailable(
        WalletRecentVisibleEvent candidate,
        bool hasLevelChange,
        WalletPointsExpiringMessage? pointsExpiring,
        WalletBirthdayBenefitMessage? birthdayBenefit,
        WalletPointCampaignMessage? pointCampaign,
        WalletMonthlyProductMessage? monthlyProduct) =>
        candidate.Type switch
        {
            NotificationType.LevelChanged => hasLevelChange,
            NotificationType.BirthdayBenefitStarted => birthdayBenefit is not null,
            NotificationType.PointsExpiring => pointsExpiring is not null,
            NotificationType.MonthlyProductStarted => monthlyProduct is not null,
            NotificationType.PointCampaignStarted => pointCampaign?.NotificationId == candidate.NotificationId,
            _ => false
        };

    private static int RecentEventPriority(NotificationType type) => type switch
    {
        NotificationType.LevelChanged => 1,
        NotificationType.BirthdayBenefitStarted => 2,
        NotificationType.PointsExpiring => 3,
        NotificationType.MonthlyProductStarted => 4,
        NotificationType.PointCampaignStarted => 5,
        NotificationType.Custom => 6,
        _ => 99
    };

    private async Task<WalletBirthdayBenefitMessage?> BuildBirthdayBenefitMessageAsync(
        Guid loyaltyCardId,
        string? metadataJson,
        CancellationToken ct)
    {
        var timeZoneId = TryReadTimeZone(metadataJson) ?? "America/Tijuana";
        var timeZone = PointsExpirationNotificationReadService.ResolveTimeZone(timeZoneId);
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date);

        var row = await (
            from card in _db.LoyaltyCards.AsNoTracking()
            join customer in _db.Customers.AsNoTracking() on card.CustomerId equals customer.Id
            where card.Id == loyaltyCardId
               && card.IsActive
               && customer.IsActive
               && customer.DateOfBirth.Month == localDate.Month
            select new
            {
                customer.DateOfBirth
            })
            .FirstOrDefaultAsync(ct);

        if (row is null)
        {
            _logger.LogDebug("Birthday benefit wallet field skipped for card {CardId}: no active birthday period.", loyaltyCardId);
            return null;
        }

        var snapshot = ProgramConfigSnapshot.FromEntries(await _db.ProgramConfigs.AsNoTracking().ToListAsync(ct));
        var multiplier = Math.Max(1, snapshot.BirthdayMultiplier);
        var multiplierText = FormatBirthdayMultiplier(multiplier);
        var displayUntilLocalDate = new DateOnly(localDate.Year, localDate.Month, DateTime.DaysInMonth(localDate.Year, localDate.Month));

        return new WalletBirthdayBenefitMessage(
            localDate.Year,
            multiplier,
            displayUntilLocalDate,
            multiplierText,
            "\ud83c\udf82 Tu beneficio de cumplea\u00f1os est\u00e1 activo: %@",
            $"Este mes obtienes {multiplierText} en todas tus compras.");
    }

    private async Task<WalletMonthlyProductMessage?> BuildMonthlyProductMessageAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var product = await _db.RewardCatalogItems
            .AsNoTracking()
            .Where(r => r.IsMonthlyProduct
                     && r.IsActive
                     && r.ValidFrom.HasValue
                     && r.ValidTo.HasValue
                     && r.ValidFrom.Value <= now
                     && r.ValidTo.Value >= now)
            .OrderBy(r => r.ValidFrom)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.PointsCost,
                ValidToUtc = r.ValidTo!.Value
            })
            .FirstOrDefaultAsync(ct);

        if (product is null)
        {
            _logger.LogDebug("Monthly product wallet field skipped: no active monthly product.");
            return null;
        }

        var timeZone = PointsExpirationNotificationReadService.ResolveTimeZone("America/Tijuana");
        var validToLocalDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(product.ValidToUtc, timeZone).Date);
        return new WalletMonthlyProductMessage(
            product.Id,
            product.Name,
            product.PointsCost,
            product.ValidToUtc,
            validToLocalDate,
            product.Name,
            "\ud83c\udf81 Nuevo Producto del mes: %@",
            $"{product.Name}\n\n{product.PointsCost:N0} pts\n\nDisponible hasta {FormatDate(validToLocalDate)}");
    }

    private async Task<WalletPointCampaignMessage?> BuildPointCampaignMessageAsync(
        Guid loyaltyCardId,
        IEnumerable<WalletNotificationMessage> notifications,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var card = await (
            from loyaltyCard in _db.LoyaltyCards.AsNoTracking()
            join customer in _db.Customers.AsNoTracking() on loyaltyCard.CustomerId equals customer.Id
            where loyaltyCard.Id == loyaltyCardId
               && loyaltyCard.IsActive
               && customer.IsActive
            select new
            {
                loyaltyCard.SerialNumber,
                loyaltyCard.Level
            })
            .FirstOrDefaultAsync(ct);

        if (card is null)
        {
            _logger.LogInformation("Point campaign wallet field omitted for card {CardId}: inactive or missing card/customer.", loyaltyCardId);
            return null;
        }

        var activeCampaigns = await _db.PointCampaigns
            .AsNoTracking()
            .Where(c => c.IsActive
                     && c.StartsAtUtc <= now
                     && c.EndsAtUtc >= now)
            .ToListAsync(ct);
        var bestCampaign = PointCampaignNotificationReadService.SelectBestCampaign(activeCampaigns, card.Level);

        if (bestCampaign is null)
        {
            _logger.LogInformation(
                "Point campaign wallet field omitted for serial {Serial}: no active eligible campaign for level {Level}.",
                card.SerialNumber,
                card.Level);
            return null;
        }

        var matchingNotification = notifications.FirstOrDefault(n =>
            TryReadPointCampaignId(n.MetadataJson, out var campaignId) &&
            campaignId == bestCampaign.Id);

        if (matchingNotification is null)
        {
            _logger.LogInformation(
                "Point campaign wallet field omitted for serial {Serial}: best campaign {CampaignId} has no active processed notification.",
                card.SerialNumber,
                bestCampaign.Id);
            return null;
        }

        var timeZoneId = TryReadTimeZone(matchingNotification.MetadataJson) ?? "America/Tijuana";
        var timeZone = PointsExpirationNotificationReadService.ResolveTimeZone(timeZoneId);
        var endsAtLocalDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(bestCampaign.EndsAtUtc, timeZone).Date);
        var value = FormatMultiplier(bestCampaign.Multiplier);
        var backValue = $"{bestCampaign.Name}\n\n{value}\n\nVigente hasta {FormatDate(endsAtLocalDate)}";

        if (bestCampaign.MinimumPurchaseAmount.HasValue)
            backValue += $"\n\nCompra m\u00ednima: ${bestCampaign.MinimumPurchaseAmount.Value:N0} MXN";

        _logger.LogInformation(
            "Point campaign wallet field included for serial {Serial}: campaign={CampaignId}, value={Value}.",
            card.SerialNumber,
            bestCampaign.Id,
            value);

        return new WalletPointCampaignMessage(
            matchingNotification.Id,
            bestCampaign.Id,
            bestCampaign.Name,
            bestCampaign.Multiplier,
            bestCampaign.MinimumPurchaseAmount,
            bestCampaign.EndsAtUtc,
            endsAtLocalDate,
            value,
            "\ud83d\udd25 \u00a1Promoci\u00f3n activa! %@",
            backValue);
    }

    private static string FormatDate(DateOnly date) =>
        date.ToDateTime(TimeOnly.MinValue).ToString("dd MMM yyyy", SpanishMexico);

    private static string FormatBirthdayMultiplier(int multiplier) =>
        $"Puntos x{Math.Max(1, multiplier).ToString(CultureInfo.InvariantCulture)}";

    private static string FormatMultiplier(int multiplier) =>
        $"Puntos x{Math.Max(1, multiplier).ToString(CultureInfo.InvariantCulture)}";

    private async Task<WalletPointsExpiringMessage?> BuildPointsExpiringMessageAsync(
        Guid loyaltyCardId,
        Guid notificationId,
        string message,
        string? metadataJson,
        CancellationToken ct)
    {
        if (!TryReadPointsExpirationMetadata(metadataJson, out var expirationDate, out var timeZoneId))
            return null;

        var (startUtc, endUtc) = PointsExpirationNotificationReadService.GetLocalDateUtcWindow(expirationDate, timeZoneId);
        var points = await _db.PointLots
            .AsNoTracking()
            .Where(l => l.LoyaltyCardId == loyaltyCardId
                     && l.RemainingAmount > 0
                     && l.ExpiresAt >= startUtc
                     && l.ExpiresAt < endUtc)
            .SumAsync(l => (int?)l.RemainingAmount, ct) ?? 0;

        return points <= 0
            ? null
            : new WalletPointsExpiringMessage(
                notificationId,
                points,
                expirationDate,
                $"{points:N0} pts",
                "\u26a0\ufe0f %@ vencer\u00e1n pronto.",
                message);
    }

    private static bool TryReadPointsExpirationMetadata(
        string? metadataJson,
        out DateOnly expirationDate,
        out string timeZoneId)
    {
        expirationDate = default;
        timeZoneId = "America/Tijuana";

        if (string.IsNullOrWhiteSpace(metadataJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("expirationDate", out var dateProp) ||
                !DateOnly.TryParse(dateProp.GetString(), out expirationDate))
            {
                return false;
            }

            if (root.TryGetProperty("timeZoneId", out var timeZoneProp) &&
                !string.IsNullOrWhiteSpace(timeZoneProp.GetString()))
            {
                timeZoneId = timeZoneProp.GetString()!;
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadPointCampaignId(string? metadataJson, out Guid campaignId)
    {
        campaignId = Guid.Empty;

        if (string.IsNullOrWhiteSpace(metadataJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            return doc.RootElement.TryGetProperty("campaignId", out var prop) &&
                   Guid.TryParse(prop.GetString(), out campaignId);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? TryReadTimeZone(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            return doc.RootElement.TryGetProperty("timeZoneId", out var prop) &&
                   !string.IsNullOrWhiteSpace(prop.GetString())
                ? prop.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
