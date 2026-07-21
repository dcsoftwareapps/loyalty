using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Campaigns.Commands.ActivatePointCampaign;

public sealed record ActivatePointCampaignCommand(Guid Id) : IRequest<Result<PointCampaignAdminDto>>;
