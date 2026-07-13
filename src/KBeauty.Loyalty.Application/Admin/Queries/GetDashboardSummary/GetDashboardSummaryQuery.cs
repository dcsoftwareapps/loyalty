using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Admin.Queries.GetDashboardSummary;

public sealed record GetDashboardSummaryQuery : IRequest<Result<DashboardSummaryDto>>;
