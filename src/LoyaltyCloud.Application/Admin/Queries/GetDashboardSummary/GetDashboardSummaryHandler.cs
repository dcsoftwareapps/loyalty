using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Admin.Queries.GetDashboardSummary;

public sealed class GetDashboardSummaryHandler
    : IRequestHandler<GetDashboardSummaryQuery, Result<DashboardSummaryDto>>
{
    private readonly IDashboardReadService _read;

    public GetDashboardSummaryHandler(IDashboardReadService read) => _read = read;

    public async Task<Result<DashboardSummaryDto>> Handle(GetDashboardSummaryQuery query, CancellationToken ct)
    {
        var dto = await _read.GetDashboardSummaryAsync(ct);
        return Result.Ok(dto);
    }
}
