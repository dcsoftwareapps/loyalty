using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Repositories;
using KBeauty.Loyalty.Application.Notifications.Custom.Commands.ProcessCustomNotificationCampaign;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Custom.Commands.ProcessDueCustomNotificationCampaigns;

public sealed class ProcessDueCustomNotificationCampaignsHandler
    : IRequestHandler<ProcessDueCustomNotificationCampaignsCommand, Result<int>>
{
    private readonly ICustomNotificationCampaignRepository _campaigns;
    private readonly IDateTimeProvider _dt;
    private readonly ISender _sender;

    public ProcessDueCustomNotificationCampaignsHandler(
        ICustomNotificationCampaignRepository campaigns,
        IDateTimeProvider dt,
        ISender sender)
    {
        _campaigns = campaigns;
        _dt = dt;
        _sender = sender;
    }

    public async Task<Result<int>> Handle(ProcessDueCustomNotificationCampaignsCommand command, CancellationToken ct)
    {
        try
        {
            var due = await _campaigns.GetDueAsync(_dt.UtcNow, command.BatchSize, ct);
            foreach (var campaign in due)
            {
                var result = await _sender.Send(new ProcessCustomNotificationCampaignCommand(campaign.Id), ct);
                if (result.IsFailure)
                    return Result.Fail<int>(result.Errors);
            }

            return Result.Ok(due.Count);
        }
        catch (Exception ex)
        {
            return Result.Fail<int>(ex.Message);
        }
    }
}
