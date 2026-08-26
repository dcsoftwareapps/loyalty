using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Admin.Queries.GetReportsSummary;

public sealed class GetReportsSummaryHandler
    : IRequestHandler<GetReportsSummaryQuery, Result<ReportsSummaryDto>>
{
    private readonly IReportsReadService _read;

    public GetReportsSummaryHandler(IReportsReadService read) => _read = read;

    public async Task<Result<ReportsSummaryDto>> Handle(GetReportsSummaryQuery query, CancellationToken ct)
    {
        if (query.PeriodStartUtc >= query.PeriodEndUtc)
            return Result.Fail<ReportsSummaryDto>("La fecha de inicio debe ser anterior a la fecha fin.");

        if (query.InactiveDaysThreshold <= 0)
            return Result.Fail<ReportsSummaryDto>("El periodo de inactividad debe ser mayor a 0 días.");

        var dto = await _read.GetReportsSummaryAsync(query, ct);
        return Result.Ok(dto);
    }
}
