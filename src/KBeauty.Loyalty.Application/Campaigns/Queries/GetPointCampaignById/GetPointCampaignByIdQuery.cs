using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Campaigns.Queries.GetPointCampaignById;

public sealed record GetPointCampaignByIdQuery(Guid Id) : IRequest<Result<PointCampaignAdminDto>>;
