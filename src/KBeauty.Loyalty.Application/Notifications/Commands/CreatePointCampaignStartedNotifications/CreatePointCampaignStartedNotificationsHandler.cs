using System.Globalization;
using System.Text.Json;
using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Application.Notifications.PointCampaign;
using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KBeauty.Loyalty.Application.Notifications.Commands.CreatePointCampaignStartedNotifications;

public sealed class CreatePointCampaignStartedNotificationsHandler
    : IRequestHandler<CreatePointCampaignStartedNotificationsCommand, Result<CreatePointCampaignStartedNotificationsResponse>>
{
    private static readonly CultureInfo SpanishMexico = CultureInfo.GetCultureInfo("es-MX");

    private readonly IPointCampaignNotificationReadService _read;
    private readonly ILoyaltyNotificationService _notifications;
    private readonly IDateTimeProvider _dt;
    private readonly ILogger<CreatePointCampaignStartedNotificationsHandler> _logger;

    public CreatePointCampaignStartedNotificationsHandler(
        IPointCampaignNotificationReadService read,
        ILoyaltyNotificationService notifications,
        IDateTimeProvider dt,
        ILogger<CreatePointCampaignStartedNotificationsHandler> logger)
    {
        _read = read;
        _notifications = notifications;
        _dt = dt;
        _logger = logger;
    }

    public async Task<Result<CreatePointCampaignStartedNotificationsResponse>> Handle(
        CreatePointCampaignStartedNotificationsCommand command,
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
                _logger.LogInformation(
                    "Point campaign notification already exists: serial={Serial}, campaign={CampaignId}, correlation={CorrelationId}.",
                    candidate.SerialNumber,
                    candidate.CampaignId,
                    candidate.CorrelationId);
                continue;
            }

            var metadata = JsonSerializer.Serialize(new
            {
                campaignId = candidate.CampaignId,
                campaignName = candidate.CampaignName,
                multiplier = candidate.Multiplier,
                minimumPurchaseAmount = candidate.MinimumPurchaseAmount,
                levelEligibility = candidate.LevelEligibility.ToString(),
                startsAtUtc = candidate.StartsAtUtc,
                endsAtUtc = candidate.EndsAtUtc,
                endsAtLocal = candidate.EndsAtLocal.ToString("O", CultureInfo.InvariantCulture),
                timeZoneId = command.TimeZoneId
            });

            try
            {
                await _notifications.CreateAsync(new CreateLoyaltyNotificationRequest(
                    candidate.SerialNumber,
                    NotificationType.PointCampaignStarted,
                    "Campa\u00f1a de puntos activa",
                    BuildMessage(candidate),
                    ScheduledAtUtc: null,
                    DisplayUntilUtc: candidate.EndsAtUtc,
                    Channels: [NotificationChannel.AppleWallet],
                    CorrelationId: candidate.CorrelationId,
                    Source: "loyalty-maintenance",
                    MetadataJson: metadata,
                    ProcessImmediately: true), ct);

                created++;
                _logger.LogInformation(
                    "Point campaign notification created: serial={Serial}, campaign={CampaignId}, multiplier={Multiplier}.",
                    candidate.SerialNumber,
                    candidate.CampaignId,
                    candidate.Multiplier);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "No se pudo crear aviso de campana de puntos para serial {Serial} y campaign {CampaignId}.",
                    candidate.SerialNumber,
                    candidate.CampaignId);
                warnings.Add($"No se pudo crear aviso de campana para serial {candidate.SerialNumber}.");
            }
        }

        return Result.Ok(new CreatePointCampaignStartedNotificationsResponse(
            _dt.UtcNow,
            preview.ActiveCampaignsFound,
            preview.CardsEvaluated,
            preview.CardsEligible,
            created,
            alreadyNotified,
            warnings.AsReadOnly()));
    }

    private static string BuildMessage(PointCampaignNotificationCandidateDto candidate)
    {
        var multiplier = FormatMultiplier(candidate.Multiplier);
        var endDate = FormatDate(DateOnly.FromDateTime(candidate.EndsAtLocal));
        var condition = candidate.MinimumPurchaseAmount.HasValue
            ? $" en compras desde ${candidate.MinimumPurchaseAmount.Value:N0} MXN"
            : string.Empty;

        return $"\ud83d\udd25 \u00a1Promoci\u00f3n activa!\n\nObt\u00e9n {multiplier}{condition} hasta el {endDate}.";
    }

    private static string FormatMultiplier(int multiplier) =>
        $"Puntos x{Math.Max(1, multiplier).ToString(CultureInfo.InvariantCulture)}";

    private static string FormatDate(DateOnly date) =>
        date.ToDateTime(TimeOnly.MinValue).ToString("dd MMM yyyy", SpanishMexico);
}
