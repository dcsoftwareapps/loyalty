using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Admin.Queries.GetDashboardSummary;

public sealed record GetDashboardSummaryQuery : IRequest<Result<DashboardSummaryDto>>;
