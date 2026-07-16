using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Application.Notifications.BirthdayBenefit;
using KBeauty.Loyalty.Domain.Enums;
using KBeauty.Loyalty.Domain.ValueObjects;
using KBeauty.Loyalty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KBeauty.Loyalty.Infrastructure.Services;

internal sealed class BirthdayBenefitNotificationReadService : IBirthdayBenefitNotificationReadService
{
    private readonly AppDbContext _db;
    private readonly ILogger<BirthdayBenefitNotificationReadService> _logger;

    public BirthdayBenefitNotificationReadService(
        AppDbContext db,
        ILogger<BirthdayBenefitNotificationReadService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<BirthdayBenefitNotificationPreviewDto> ListCandidatesAsync(
        string timeZoneId,
        bool includeAlreadyNotified,
        CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;
        var timeZone = PointsExpirationNotificationReadService.ResolveTimeZone(timeZoneId);
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone).Date);
        var displayUntilUtc = GetEndOfBirthdayMonthUtc(localDate, timeZone);
        var snapshot = ProgramConfigSnapshot.FromEntries(await _db.ProgramConfigs.AsNoTracking().ToListAsync(ct));
        var multiplier = Math.Max(1, snapshot.BirthdayMultiplier);

        var eligibleCards = await (
            from card in _db.LoyaltyCards.AsNoTracking()
            join customer in _db.Customers.AsNoTracking() on card.CustomerId equals customer.Id
            where card.IsActive
               && customer.IsActive
               && customer.DateOfBirth.Month == localDate.Month
               && _db.DeviceRegistrations.AsNoTracking().Any(d => d.SerialNumber == card.SerialNumber)
            select new
            {
                CustomerId = customer.Id,
                LoyaltyCardId = card.Id,
                CustomerName = customer.FullName,
                card.SerialNumber,
                customer.DateOfBirth
            })
            .OrderBy(x => x.CustomerName)
            .ThenBy(x => x.SerialNumber)
            .ToListAsync(ct);

        var correlations = eligibleCards
            .Select(x => BuildCorrelationId(x.SerialNumber, localDate.Year))
            .ToArray();
        var existingRows = correlations.Length == 0
            ? new List<string>()
            : await _db.LoyaltyNotifications
                .AsNoTracking()
                .Where(n => n.Type == NotificationType.BirthdayBenefitStarted
                         && n.CorrelationId != null
                         && correlations.Contains(n.CorrelationId))
                .Select(n => n.CorrelationId!)
                .ToListAsync(ct);
        var existing = existingRows.ToHashSet(StringComparer.Ordinal);
        var displayUntilLocalDate = new DateOnly(localDate.Year, localDate.Month, DateTime.DaysInMonth(localDate.Year, localDate.Month));

        var candidates = eligibleCards
            .Select(x =>
            {
                var correlationId = BuildCorrelationId(x.SerialNumber, localDate.Year);
                return new BirthdayBenefitNotificationCandidateDto(
                    x.CustomerId,
                    x.LoyaltyCardId,
                    x.CustomerName,
                    x.SerialNumber,
                    x.DateOfBirth,
                    localDate.Year,
                    multiplier,
                    displayUntilUtc,
                    displayUntilLocalDate,
                    correlationId,
                    existing.Contains(correlationId));
            })
            .Where(x => includeAlreadyNotified || !x.AlreadyNotified)
            .ToList();

        _logger.LogInformation(
            "Birthday benefit notification preview: localDate={LocalDate}, eligibleCustomers={EligibleCustomers}, candidatesReturned={CandidatesReturned}, alreadyNotified={AlreadyNotified}, multiplier={Multiplier}.",
            localDate,
            eligibleCards.Count,
            candidates.Count,
            eligibleCards.Count - candidates.Count,
            multiplier);

        return new BirthdayBenefitNotificationPreviewDto(
            nowUtc,
            localDate,
            eligibleCards.Count,
            multiplier,
            displayUntilUtc,
            displayUntilLocalDate,
            candidates.AsReadOnly());
    }

    internal static string BuildCorrelationId(string serialNumber, int benefitYear) =>
        $"birthday-benefit:{serialNumber}:{benefitYear}";

    internal static DateTime GetEndOfBirthdayMonthUtc(DateOnly localDate, TimeZoneInfo timeZone)
    {
        var nextMonth = localDate.Month == 12
            ? new DateOnly(localDate.Year + 1, 1, 1)
            : new DateOnly(localDate.Year, localDate.Month + 1, 1);

        var nextMonthStart = DateTime.SpecifyKind(nextMonth.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(nextMonthStart, timeZone);
    }
}
