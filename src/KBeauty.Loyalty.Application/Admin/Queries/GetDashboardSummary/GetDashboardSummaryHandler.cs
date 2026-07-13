using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Admin.Queries.GetDashboardSummary;

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
