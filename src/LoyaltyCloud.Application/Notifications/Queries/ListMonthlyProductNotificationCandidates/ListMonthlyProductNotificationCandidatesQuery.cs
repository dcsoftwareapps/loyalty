using LoyaltyCloud.Application.Notifications.MonthlyProduct;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Queries.ListMonthlyProductNotificationCandidates;

public sealed record ListMonthlyProductNotificationCandidatesQuery(
    string TimeZoneId = "America/Tijuana",
    bool IncludeAlreadyNotified = false) : IRequest<Result<MonthlyProductNotificationPreviewDto>>;
