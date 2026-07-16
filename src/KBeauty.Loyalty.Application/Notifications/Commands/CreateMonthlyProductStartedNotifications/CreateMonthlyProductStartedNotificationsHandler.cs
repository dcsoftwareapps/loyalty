using System.Globalization;
using System.Text.Json;
using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Application.Notifications.MonthlyProduct;
using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KBeauty.Loyalty.Application.Notifications.Commands.CreateMonthlyProductStartedNotifications;

public sealed class CreateMonthlyProductStartedNotificationsHandler
    : IRequestHandler<CreateMonthlyProductStartedNotificationsCommand, Result<CreateMonthlyProductStartedNotificationsResponse>>
{
    private static readonly CultureInfo SpanishMexico = CultureInfo.GetCultureInfo("es-MX");

    private readonly IMonthlyProductNotificationReadService _read;
    private readonly ILoyaltyNotificationService _notifications;
    private readonly IDateTimeProvider _dt;
    private readonly ILogger<CreateMonthlyProductStartedNotificationsHandler> _logger;

    public CreateMonthlyProductStartedNotificationsHandler(
        IMonthlyProductNotificationReadService read,
        ILoyaltyNotificationService notifications,
        IDateTimeProvider dt,
        ILogger<CreateMonthlyProductStartedNotificationsHandler> logger)
    {
        _read = read;
        _notifications = notifications;
        _dt = dt;
        _logger = logger;
    }

    public async Task<Result<CreateMonthlyProductStartedNotificationsResponse>> Handle(
        CreateMonthlyProductStartedNotificationsCommand command,
        CancellationToken ct)
    {
        var warnings = new List<string>();
        var preview = await _read.ListCandidatesAsync(
            command.TimeZoneId,
            includeAlreadyNotified: true,
            ct);

        if (preview.MonthlyProductId is null)
        {
            _logger.LogInformation("No active monthly product was found for wallet notification scan.");
            return Result.Ok(new CreateMonthlyProductStartedNotificationsResponse(
                _dt.UtcNow,
                null,
                null,
                0,
                0,
                0,
                warnings.AsReadOnly()));
        }

        var created = 0;
        var alreadyNotified = 0;

        foreach (var candidate in preview.Candidates)
        {
            if (candidate.AlreadyNotified)
            {
                alreadyNotified++;
                continue;
            }

            var metadata = JsonSerializer.Serialize(new
            {
                rewardId = candidate.MonthlyProductId,
                productName = candidate.MonthlyProductName,
                pointsCost = candidate.PointsCost,
                validToUtc = candidate.ValidToUtc,
                validToLocalDate = candidate.ValidToLocalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                timeZoneId = command.TimeZoneId
            });

            try
            {
                await _notifications.CreateAsync(new CreateLoyaltyNotificationRequest(
                    candidate.SerialNumber,
                    NotificationType.MonthlyProductStarted,
                    "Nuevo Producto del mes",
                    BuildMessage(candidate),
                    ScheduledAtUtc: null,
                    DisplayUntilUtc: candidate.ValidToUtc,
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
                    "No se pudo crear aviso de Producto del mes para serial {Serial} y reward {RewardId}.",
                    candidate.SerialNumber,
                    candidate.MonthlyProductId);
                warnings.Add($"No se pudo crear aviso de Producto del mes para serial {candidate.SerialNumber}.");
            }
        }

        return Result.Ok(new CreateMonthlyProductStartedNotificationsResponse(
            _dt.UtcNow,
            preview.MonthlyProductId,
            preview.MonthlyProductName,
            preview.CardsEligible,
            created,
            alreadyNotified,
            warnings.AsReadOnly()));
    }

    private static string BuildMessage(MonthlyProductNotificationCandidateDto candidate) =>
        $"\ud83c\udf81 {candidate.MonthlyProductName} ya est\u00e1 disponible.\n\n" +
        $"Canj\u00e9alo por {candidate.PointsCost:N0} puntos antes del {FormatDate(candidate.ValidToLocalDate)}.";

    private static string FormatDate(DateOnly date) =>
        date.ToDateTime(TimeOnly.MinValue).ToString("dd MMM yyyy", SpanishMexico);
}
