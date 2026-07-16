using KBeauty.Loyalty.Application.Notifications.MonthlyProduct;
using KBeauty.Loyalty.Common.Results;
using MediatR;

namespace KBeauty.Loyalty.Application.Notifications.Queries.ListMonthlyProductNotificationCandidates;

public sealed record ListMonthlyProductNotificationCandidatesQuery(
    string TimeZoneId = "America/Tijuana",
    bool IncludeAlreadyNotified = false) : IRequest<Result<MonthlyProductNotificationPreviewDto>>;
