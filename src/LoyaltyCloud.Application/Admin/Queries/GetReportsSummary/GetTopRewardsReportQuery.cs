using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Admin.Queries.GetReportsSummary;

public sealed record GetTopRewardsReportQuery(
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    int Limit = 5) : IRequest<Result<IReadOnlyList<ReportsTopRewardDto>>>;
