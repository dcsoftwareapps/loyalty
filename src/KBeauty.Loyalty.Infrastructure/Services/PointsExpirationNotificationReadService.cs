using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Application.Notifications.PointsExpiration;
using KBeauty.Loyalty.Domain.Enums;
using KBeauty.Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KBeauty.Loyalty.Infrastructure.Services;

internal sealed class PointsExpirationNotificationReadService : IPointsExpirationNotificationReadService
{
    private readonly AppDbContext _db;

    public PointsExpirationNotificationReadService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PointsExpirationNotificationCandidateDto>> ListCandidatesAsync(
        int daysAhead,
        string timeZoneId,
        bool includeAlreadyNotified,
        CancellationToken ct = default)
    {
        if (daysAhead <= 0)
            throw new InvalidOperationException("DaysAhead debe ser mayor a cero.");

        var timeZone = ResolveTimeZone(timeZoneId);
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
        var targetLocalDate = DateOnly.FromDateTime(nowLocal.AddDays(daysAhead));
        var startUtc = ToUtc(targetLocalDate.ToDateTime(TimeOnly.MinValue), timeZone);
        var endUtc = ToUtc(targetLocalDate.AddDays(1).ToDateTime(TimeOnly.MinValue), timeZone);

        var groupedLots = await (
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
                customer.FullName,
                card.SerialNumber
            }
            into g
            select new
            {
                g.Key.CustomerId,
                g.Key.LoyaltyCardId,
                CustomerName = g.Key.FullName,
                g.Key.SerialNumber,
                PointsExpiring = g.Sum(l => l.RemainingAmount)
            })
            .OrderBy(x => x.CustomerName)
            .ThenBy(x => x.SerialNumber)
            .ToListAsync(ct);

        var correlations = groupedLots
            .Select(x => BuildCorrelationId(x.SerialNumber, targetLocalDate))
            .ToArray();
        var existingRows = correlations.Length == 0
            ? new List<string>()
            : await _db.LoyaltyNotifications
                .AsNoTracking()
                .Where(n => n.Type == NotificationType.PointsExpiring
                         && n.CorrelationId != null
                         && correlations.Contains(n.CorrelationId))
                .Select(n => n.CorrelationId!)
                .ToListAsync(ct);
        var existing = existingRows.ToHashSet(StringComparer.Ordinal);

        var result = groupedLots
            .Select(x =>
            {
                var correlationId = BuildCorrelationId(x.SerialNumber, targetLocalDate);
                return new PointsExpirationNotificationCandidateDto(
                    x.CustomerId,
                    x.LoyaltyCardId,
                    x.CustomerName,
                    x.SerialNumber,
                    targetLocalDate,
                    x.PointsExpiring,
                    correlationId,
                    existing.Contains(correlationId));
            })
            .Where(x => includeAlreadyNotified || !x.AlreadyNotified)
            .ToList();

        return result.AsReadOnly();
    }

    internal static string BuildCorrelationId(string serialNumber, DateOnly expirationDate) =>
        $"points-expiring:{serialNumber}:{expirationDate:yyyyMMdd}";

    internal static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
        }
        catch (InvalidTimeZoneException)
        {
        }

        if (string.Equals(timeZoneId, "America/Tijuana", StringComparison.OrdinalIgnoreCase))
            return TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time (Mexico)");

        throw new InvalidOperationException($"Zona horaria invalida: {timeZoneId}.");
    }

    internal static (DateTime StartUtc, DateTime EndUtc) GetLocalDateUtcWindow(DateOnly localDate, string timeZoneId)
    {
        var timeZone = ResolveTimeZone(timeZoneId);
        return (
            ToUtc(localDate.ToDateTime(TimeOnly.MinValue), timeZone),
            ToUtc(localDate.AddDays(1).ToDateTime(TimeOnly.MinValue), timeZone));
    }

    private static DateTime ToUtc(DateTime localDateTime, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified), timeZone);
}
