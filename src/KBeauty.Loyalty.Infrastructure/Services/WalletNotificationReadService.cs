using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Domain.Enums;
using KBeauty.Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;

namespace KBeauty.Loyalty.Infrastructure.Services;

internal sealed class WalletNotificationReadService : IWalletNotificationReadService
{
    private readonly AppDbContext _db;
    private readonly ILogger<WalletNotificationReadService> _logger;
    private static readonly CultureInfo SpanishMexico = CultureInfo.GetCultureInfo("es-MX");

    public WalletNotificationReadService(
        AppDbContext db,
        ILogger<WalletNotificationReadService> logger)
    {
        _db = db;
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
            .Select(n => new { n.Id, n.Type, n.Title, n.Message, n.MetadataJson })
            .Take(10)
            .ToListAsync(ct);

        var news = rows.FirstOrDefault();
        var levelChange = rows.FirstOrDefault(n => n.Type == NotificationType.LevelChanged);
        var pointsExpiring = rows.FirstOrDefault(n => n.Type == NotificationType.PointsExpiring);

        return new WalletNotificationContext(
            news is null
                ? null
                : new WalletNotificationMessage(news.Id, news.Type, news.Title, news.Message, news.MetadataJson),
            levelChange is null
                ? null
                : new WalletNotificationMessage(levelChange.Id, levelChange.Type, levelChange.Title, levelChange.Message, levelChange.MetadataJson),
            pointsExpiring is null
                ? null
                : await BuildPointsExpiringMessageAsync(loyaltyCardId, pointsExpiring.Id, pointsExpiring.Message, pointsExpiring.MetadataJson, ct),
            await BuildMonthlyProductMessageAsync(ct));
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

    private static string FormatDate(DateOnly date) =>
        date.ToDateTime(TimeOnly.MinValue).ToString("dd MMM yyyy", SpanishMexico);

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
}
