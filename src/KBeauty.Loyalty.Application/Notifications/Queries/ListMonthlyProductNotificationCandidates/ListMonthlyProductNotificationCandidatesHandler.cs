using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Application.Notifications.MonthlyProduct;
using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Queries.ListMonthlyProductNotificationCandidates;

public sealed class ListMonthlyProductNotificationCandidatesHandler
    : IRequestHandler<ListMonthlyProductNotificationCandidatesQuery, Result<MonthlyProductNotificationPreviewDto>>
{
    private readonly IMonthlyProductNotificationReadService _read;

    public ListMonthlyProductNotificationCandidatesHandler(IMonthlyProductNotificationReadService read) => _read = read;

    public async Task<Result<MonthlyProductNotificationPreviewDto>> Handle(
        ListMonthlyProductNotificationCandidatesQuery query,
        CancellationToken ct)
    {
        try
        {
            var preview = await _read.ListCandidatesAsync(
                query.TimeZoneId,
                query.IncludeAlreadyNotified,
                ct);

            return Result.Ok(preview);
        }
        catch (Exception ex)
        {
            return Result.Fail<MonthlyProductNotificationPreviewDto>(ex.Message);
        }
    }
}
