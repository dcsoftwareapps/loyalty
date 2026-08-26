using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Admin.Queries.GetReportsSummary;

public sealed class GetInactiveCustomersReportHandler
    : IRequestHandler<GetInactiveCustomersReportQuery, Result<ReportsInactiveCustomersDto>>
{
    private readonly IReportsReadService _read;

    public GetInactiveCustomersReportHandler(IReportsReadService read) => _read = read;

    public async Task<Result<ReportsInactiveCustomersDto>> Handle(GetInactiveCustomersReportQuery query, CancellationToken ct)
    {
        if (query.InactiveDaysThreshold <= 0)
            return Result.Fail<ReportsInactiveCustomersDto>("El periodo de inactividad debe ser mayor a 0 días.");

        var dto = await _read.GetInactiveCustomersAsync(query, ct);
        return Result.Ok(dto);
    }
}
