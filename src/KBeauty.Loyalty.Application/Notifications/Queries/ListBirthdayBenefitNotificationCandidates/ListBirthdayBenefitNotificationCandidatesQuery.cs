using KBeauty.Loyalty.Application.Notifications.BirthdayBenefit;
using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Queries.ListBirthdayBenefitNotificationCandidates;

public sealed record ListBirthdayBenefitNotificationCandidatesQuery(
    string TimeZoneId = "America/Tijuana",
    bool IncludeAlreadyNotified = false) : IRequest<Result<BirthdayBenefitNotificationPreviewDto>>;
