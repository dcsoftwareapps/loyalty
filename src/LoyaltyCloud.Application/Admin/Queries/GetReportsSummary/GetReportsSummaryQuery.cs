using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Admin.Queries.GetReportsSummary;

public sealed record GetReportsSummaryQuery(
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    int InactiveDaysThreshold,
    int TopRewardsLimit = 5,
    int InactiveCustomersLimit = 25) : IRequest<Result<ReportsSummaryDto>>;
