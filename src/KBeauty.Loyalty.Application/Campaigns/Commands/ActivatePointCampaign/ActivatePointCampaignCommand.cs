using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Campaigns.Commands.ActivatePointCampaign;

public sealed record ActivatePointCampaignCommand(Guid Id) : IRequest<Result<PointCampaignAdminDto>>;
