using System.Text.Json;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Notifications.Custom;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Application.Notifications.Custom.Commands.ProcessCustomNotificationCampaign;

public sealed class ProcessCustomNotificationCampaignHandler
    : IRequestHandler<ProcessCustomNotificationCampaignCommand, Result<CustomNotificationCampaignProcessingDto>>
{
    private readonly ICustomNotificationCampaignRepository _campaigns;
    private readonly ILoyaltyNotificationRepository _notifications;
    private readonly ICustomNotificationAudienceReadService _audience;
    private readonly ILoyaltyNotificationService _notificationService;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _dt;
    private readonly ILogger<ProcessCustomNotificationCampaignHandler> _logger;

    public ProcessCustomNotificationCampaignHandler(
        ICustomNotificationCampaignRepository campaigns,
        ILoyaltyNotificationRepository notifications,
        ICustomNotificationAudienceReadService audience,
        ILoyaltyNotificationService notificationService,
        IUnitOfWork uow,
        IDateTimeProvider dt,
        ILogger<ProcessCustomNotificationCampaignHandler> logger)
    {
        _campaigns = campaigns;
        _notifications = notifications;
        _audience = audience;
        _notificationService = notificationService;
        _uow = uow;
        _dt = dt;
        _logger = logger;
    }

    public async Task<Result<CustomNotificationCampaignProcessingDto>> Handle(
        ProcessCustomNotificationCampaignCommand command,
        CancellationToken ct)
    {
        var warnings = new List<string>();

        try
        {
            var campaign = await _campaigns.GetByIdAsync(command.CampaignId, ct);
            if (campaign is null)
                return Result.Fail<CustomNotificationCampaignProcessingDto>("Campana personalizada no encontrada.");

            if (campaign.Status is CustomNotificationCampaignStatus.Completed
                or CustomNotificationCampaignStatus.PartiallyCompleted
                or CustomNotificationCampaignStatus.Cancelled)
            {
                return Result.Ok(ToProcessingDto(campaign, warnings));
            }

            var recipients = await _audience.ResolveRecipientsAsync(
                campaign.AudienceType,
                campaign.MinimumPoints,
                campaign.PointsExpiringDaysAhead,
                ct);

            campaign.MarkProcessing(_dt.UtcNow, recipients.Count);
            _campaigns.Update(campaign);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Processing custom notification campaign {CampaignId}. Audience={AudienceType}, recipients={RecipientCount}.",
                campaign.Id,
                campaign.AudienceType,
                recipients.Count);

            var created = 0;
            var succeeded = 0;
            var failed = 0;

            foreach (var recipient in recipients)
            {
                var correlationId = BuildCorrelationId(campaign.Id, recipient.SerialNumber);
                var existing = await _notifications.GetByCorrelationIdAsync(correlationId, ct);
                if (existing is not null)
                {
                    warnings.Add($"Notificacion duplicada omitida para serial {recipient.SerialNumber}.");
                    continue;
                }

                var metadataJson = JsonSerializer.Serialize(new
                {
                    campaignId = campaign.Id,
                    audienceType = campaign.AudienceType.ToString(),
                    shortMessage = campaign.ShortMessage,
                    longMessage = campaign.LongMessage
                });

                var dto = await _notificationService.CreateAsync(new CreateLoyaltyNotificationRequest(
                    SerialNumber: recipient.SerialNumber,
                    Type: NotificationType.Custom,
                    Title: campaign.Title,
                    Message: campaign.LongMessage,
                    ScheduledAtUtc: null,
                    DisplayUntilUtc: campaign.DisplayUntilUtc,
                    Channels: [NotificationChannel.AppleWallet],
                    CorrelationId: correlationId,
                    Source: "custom-campaign",
                    MetadataJson: metadataJson,
                    ProcessImmediately: true,
                    CustomNotificationCampaignId: campaign.Id,
                    ShortMessage: campaign.ShortMessage,
                    LongMessage: campaign.LongMessage), ct);

                created++;
                if (dto.Status is NotificationStatus.Delivered or NotificationStatus.PartiallyDelivered)
                    succeeded++;
                else if (dto.Status == NotificationStatus.Failed)
                    failed++;
            }

            var failureReason = failed > 0
                ? $"{failed} notificacion(es) fallaron durante el procesamiento."
                : null;
            campaign.MarkCompleted(_dt.UtcNow, created, succeeded, failed, failureReason);
            _campaigns.Update(campaign);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Custom notification campaign {CampaignId} completed. created={Created}, succeeded={Succeeded}, failed={Failed}.",
                campaign.Id,
                created,
                succeeded,
                failed);

            return Result.Ok(ToProcessingDto(campaign, warnings));
        }
        catch (Exception ex)
        {
            return Result.Fail<CustomNotificationCampaignProcessingDto>(ex.Message);
        }
    }

    internal static string BuildCorrelationId(Guid campaignId, string serialNumber) =>
        $"custom-campaign:{campaignId:N}:{serialNumber}";

    private static CustomNotificationCampaignProcessingDto ToProcessingDto(
        LoyaltyCloud.Domain.Entities.CustomNotificationCampaign campaign,
        IReadOnlyList<string> warnings) =>
        new(
            campaign.Id,
            campaign.Status,
            campaign.IntendedRecipients,
            campaign.NotificationsCreated,
            campaign.NotificationsSucceeded,
            campaign.NotificationsFailed,
            warnings);
}
