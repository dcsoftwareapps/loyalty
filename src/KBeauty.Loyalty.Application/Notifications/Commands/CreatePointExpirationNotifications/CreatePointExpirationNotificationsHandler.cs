using System.Text.Json;
using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Application.Notifications.PointsExpiration;
using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KBeauty.Loyalty.Application.Notifications.Commands.CreatePointExpirationNotifications;

public sealed class CreatePointExpirationNotificationsHandler
    : IRequestHandler<CreatePointExpirationNotificationsCommand, Result<CreatePointExpirationNotificationsResponse>>
{
    private readonly IPointsExpirationNotificationReadService _read;
    private readonly ILoyaltyNotificationService _notifications;
    private readonly IDateTimeProvider _dt;
    private readonly ILogger<CreatePointExpirationNotificationsHandler> _logger;

    public CreatePointExpirationNotificationsHandler(
        IPointsExpirationNotificationReadService read,
        ILoyaltyNotificationService notifications,
        IDateTimeProvider dt,
        ILogger<CreatePointExpirationNotificationsHandler> logger)
    {
        _read = read;
        _notifications = notifications;
        _dt = dt;
        _logger = logger;
    }

    public async Task<Result<CreatePointExpirationNotificationsResponse>> Handle(
        CreatePointExpirationNotificationsCommand command,
        CancellationToken ct)
    {
        var warnings = new List<string>();
        var candidates = await _read.ListCandidatesAsync(
            command.DaysAhead,
            command.TimeZoneId,
            includeAlreadyNotified: true,
            ct);

        var targetDate = candidates.Count > 0
            ? candidates[0].ExpirationDate
            : DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(_dt.UtcNow, ResolveTimeZone(command.TimeZoneId)).Date.AddDays(command.DaysAhead));

        var created = 0;
        var alreadyNotified = 0;

        foreach (var candidate in candidates)
        {
            if (candidate.AlreadyNotified)
            {
                alreadyNotified++;
                continue;
            }

            var metadata = JsonSerializer.Serialize(new
            {
                expirationDate = candidate.ExpirationDate.ToString("yyyy-MM-dd"),
                pointsExpiring = candidate.PointsExpiring,
                daysAhead = command.DaysAhead,
                timeZoneId = command.TimeZoneId
            });

            try
            {
                await _notifications.CreateAsync(new CreateLoyaltyNotificationRequest(
                    candidate.SerialNumber,
                    NotificationType.PointsExpiring,
                    "Puntos por expirar",
                    BuildMessage(candidate.PointsExpiring, command.DaysAhead),
                    ScheduledAtUtc: null,
                    DisplayUntilUtc: ToEndOfLocalDateUtc(candidate.ExpirationDate, command.TimeZoneId),
                    Channels: [NotificationChannel.AppleWallet],
                    CorrelationId: candidate.CorrelationId,
                    Source: "loyalty-maintenance",
                    MetadataJson: metadata,
                    ProcessImmediately: true), ct);

                created++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "No se pudo crear aviso de puntos por expirar para serial {Serial} y fecha {ExpirationDate}.",
                    candidate.SerialNumber,
                    candidate.ExpirationDate);
                warnings.Add($"No se pudo crear aviso para serial {candidate.SerialNumber}.");
            }
        }

        return Result.Ok(new CreatePointExpirationNotificationsResponse(
            _dt.UtcNow,
            targetDate,
            candidates.Count,
            created,
            alreadyNotified,
            warnings.AsReadOnly()));
    }

    private static string BuildMessage(int points, int daysAhead) =>
        $"\u26a0\ufe0f {points:N0} puntos vencer\u00e1n en {daysAhead} d\u00edas.\n\n\u00dasalos antes de perderlos.";

    private static DateTime ToEndOfLocalDateUtc(DateOnly date, string timeZoneId)
    {
        var timeZone = ResolveTimeZone(timeZoneId);
        var nextLocalMidnight = DateTime.SpecifyKind(date.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(nextLocalMidnight, timeZone);
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
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
}
