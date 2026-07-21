using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Campaigns.Commands.DeactivatePointCampaign;

public sealed record DeactivatePointCampaignCommand(Guid Id) : IRequest<Result<PointCampaignAdminDto>>;
