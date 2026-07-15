using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Campaigns.Queries.ListPointCampaigns;

public sealed record ListPointCampaignsQuery : IRequest<Result<IReadOnlyList<PointCampaignAdminDto>>>;
