using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Admin.Queries.GetReportsSummary;

public sealed class GetTopRewardsReportHandler
    : IRequestHandler<GetTopRewardsReportQuery, Result<IReadOnlyList<ReportsTopRewardDto>>>
{
    private readonly IReportsReadService _read;

    public GetTopRewardsReportHandler(IReportsReadService read) => _read = read;

    public async Task<Result<IReadOnlyList<ReportsTopRewardDto>>> Handle(GetTopRewardsReportQuery query, CancellationToken ct)
    {
        if (query.PeriodStartUtc >= query.PeriodEndUtc)
            return Result.Fail<IReadOnlyList<ReportsTopRewardDto>>("La fecha de inicio debe ser anterior a la fecha fin.");

        var dto = await _read.GetTopRewardsAsync(query, ct);
        return Result.Ok(dto);
    }
}
