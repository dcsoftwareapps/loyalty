using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Notifications.MonthlyProduct;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Queries.ListMonthlyProductNotificationCandidates;

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
