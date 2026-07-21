using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Campaigns.Queries.ListPointCampaigns;

public sealed record ListPointCampaignsQuery : IRequest<Result<IReadOnlyList<PointCampaignAdminDto>>>;
