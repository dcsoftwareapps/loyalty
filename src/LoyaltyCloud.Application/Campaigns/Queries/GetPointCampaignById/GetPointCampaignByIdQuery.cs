using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Campaigns.Queries.GetPointCampaignById;

public sealed record GetPointCampaignByIdQuery(Guid Id) : IRequest<Result<PointCampaignAdminDto>>;
