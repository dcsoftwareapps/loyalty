using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Admin.Queries.GetReportsSummary;

public sealed record GetInactiveCustomersReportQuery(
    int InactiveDaysThreshold,
    int Limit = 25) : IRequest<Result<ReportsInactiveCustomersDto>>;
