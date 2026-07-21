using LoyaltyCloud.Application.Notifications.Custom;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Application.Notifications.Custom.Commands.ProcessCustomNotificationCampaign;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Application.Notifications.Custom.Commands.CreateCustomNotificationCampaign;

public sealed class CreateCustomNotificationCampaignHandler
    : IRequestHandler<CreateCustomNotificationCampaignCommand, Result<CustomNotificationCampaignDto>>
{
    private readonly ICustomNotificationCampaignRepository _campaigns;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _dt;
    private readonly ISender _sender;
    private readonly ILogger<CreateCustomNotificationCampaignHandler> _logger;

    public CreateCustomNotificationCampaignHandler(
        ICustomNotificationCampaignRepository campaigns,
        IUnitOfWork uow,
        IDateTimeProvider dt,
        ISender sender,
        ILogger<CreateCustomNotificationCampaignHandler> logger)
    {
        _campaigns = campaigns;
        _uow = uow;
        _dt = dt;
        _sender = sender;
        _logger = logger;
    }

    public async Task<Result<CustomNotificationCampaignDto>> Handle(CreateCustomNotificationCampaignCommand command, CancellationToken ct)
    {
        try
        {
            var now = _dt.UtcNow;
            var scheduledAt = command.SendImmediately ? null : command.ScheduledAtUtc;
            var campaign = new CustomNotificationCampaign(
                Guid.NewGuid(),
                command.Name,
                command.Title,
                command.ShortMessage,
                command.LongMessage,
                command.AudienceType,
                command.MinimumPoints,
                command.PointsExpiringDaysAhead,
                scheduledAt,
                command.DisplayUntilUtc,
                now);

            await _campaigns.AddAsync(campaign, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Custom notification campaign {CampaignId} created. Audience={AudienceType}, ScheduledAtUtc={ScheduledAtUtc}, SendImmediately={SendImmediately}.",
                campaign.Id,
                campaign.AudienceType,
                campaign.ScheduledAtUtc,
                command.SendImmediately);

            if (command.SendImmediately)
            {
                var processed = await _sender.Send(new ProcessCustomNotificationCampaignCommand(campaign.Id), ct);
                if (processed.IsFailure)
                    return Result.Fail<CustomNotificationCampaignDto>(processed.Errors);

                var updated = await _campaigns.GetByIdAsync(campaign.Id, ct) ?? campaign;
                return Result.Ok(CustomNotificationCampaignMapper.ToDto(updated));
            }

            return Result.Ok(CustomNotificationCampaignMapper.ToDto(campaign));
        }
        catch (Exception ex)
        {
            return Result.Fail<CustomNotificationCampaignDto>(ex.Message);
        }
    }
}
