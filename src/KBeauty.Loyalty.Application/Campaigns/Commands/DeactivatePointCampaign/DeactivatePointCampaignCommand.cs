using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Campaigns.Commands.DeactivatePointCampaign;

public sealed record DeactivatePointCampaignCommand(Guid Id) : IRequest<Result<PointCampaignAdminDto>>;
