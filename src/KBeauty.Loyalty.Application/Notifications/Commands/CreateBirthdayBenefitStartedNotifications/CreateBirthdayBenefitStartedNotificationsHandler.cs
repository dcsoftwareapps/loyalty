using System.Globalization;
using System.Text.Json;
using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Application.Notifications.BirthdayBenefit;
using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KBeauty.Loyalty.Application.Notifications.Commands.CreateBirthdayBenefitStartedNotifications;

public sealed class CreateBirthdayBenefitStartedNotificationsHandler
    : IRequestHandler<CreateBirthdayBenefitStartedNotificationsCommand, Result<CreateBirthdayBenefitStartedNotificationsResponse>>
{
    private readonly IBirthdayBenefitNotificationReadService _read;
    private readonly ILoyaltyNotificationService _notifications;
    private readonly IDateTimeProvider _dt;
    private readonly ILogger<CreateBirthdayBenefitStartedNotificationsHandler> _logger;

    public CreateBirthdayBenefitStartedNotificationsHandler(
        IBirthdayBenefitNotificationReadService read,
        ILoyaltyNotificationService notifications,
        IDateTimeProvider dt,
        ILogger<CreateBirthdayBenefitStartedNotificationsHandler> logger)
    {
        _read = read;
        _notifications = notifications;
        _dt = dt;
        _logger = logger;
    }

    public async Task<Result<CreateBirthdayBenefitStartedNotificationsResponse>> Handle(
        CreateBirthdayBenefitStartedNotificationsCommand command,
        CancellationToken ct)
    {
        var warnings = new List<string>();
        var preview = await _read.ListCandidatesAsync(
            command.TimeZoneId,
            includeAlreadyNotified: true,
            ct);

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
                benefitYear = candidate.BenefitYear,
                multiplier = candidate.Multiplier,
                displayUntilUtc = candidate.DisplayUntilUtc,
                displayUntilLocalDate = candidate.DisplayUntilLocalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                timeZoneId = command.TimeZoneId
            });

            try
            {
                await _notifications.CreateAsync(new CreateLoyaltyNotificationRequest(
                    candidate.SerialNumber,
                    NotificationType.BirthdayBenefitStarted,
                    "Beneficio de cumplea\u00f1os",
                    BuildMessage(candidate.Multiplier),
                    ScheduledAtUtc: null,
                    DisplayUntilUtc: candidate.DisplayUntilUtc,
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
                    "No se pudo crear aviso de cumpleanos para serial {Serial} en anio {BenefitYear}.",
                    candidate.SerialNumber,
                    candidate.BenefitYear);
                warnings.Add($"No se pudo crear aviso de cumpleanos para serial {candidate.SerialNumber}.");
            }
        }

        return Result.Ok(new CreateBirthdayBenefitStartedNotificationsResponse(
            _dt.UtcNow,
            preview.LocalDate,
            preview.CustomersEligible,
            created,
            alreadyNotified,
            warnings.AsReadOnly()));
    }

    private static string BuildMessage(int multiplier)
    {
        return $"\ud83c\udf82 \u00a1Feliz cumplea\u00f1os!\n\nEste mes obtienes {FormatBirthdayMultiplier(multiplier)} en todas tus compras.";
    }

    private static string FormatBirthdayMultiplier(int multiplier) =>
        $"Puntos x{Math.Max(1, multiplier).ToString(CultureInfo.InvariantCulture)}";
}
