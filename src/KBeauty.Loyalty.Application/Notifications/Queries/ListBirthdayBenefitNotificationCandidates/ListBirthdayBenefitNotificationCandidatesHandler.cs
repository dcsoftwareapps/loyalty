using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Application.Notifications.BirthdayBenefit;
using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Queries.ListBirthdayBenefitNotificationCandidates;

public sealed class ListBirthdayBenefitNotificationCandidatesHandler
    : IRequestHandler<ListBirthdayBenefitNotificationCandidatesQuery, Result<BirthdayBenefitNotificationPreviewDto>>
{
    private readonly IBirthdayBenefitNotificationReadService _read;

    public ListBirthdayBenefitNotificationCandidatesHandler(IBirthdayBenefitNotificationReadService read) => _read = read;

    public async Task<Result<BirthdayBenefitNotificationPreviewDto>> Handle(
        ListBirthdayBenefitNotificationCandidatesQuery query,
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
            return Result.Fail<BirthdayBenefitNotificationPreviewDto>(ex.Message);
        }
    }
}
