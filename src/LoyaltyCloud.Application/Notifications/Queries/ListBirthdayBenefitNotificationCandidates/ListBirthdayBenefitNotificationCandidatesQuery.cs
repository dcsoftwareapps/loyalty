using LoyaltyCloud.Application.Notifications.BirthdayBenefit;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Notifications.Queries.ListBirthdayBenefitNotificationCandidates;

public sealed record ListBirthdayBenefitNotificationCandidatesQuery(
    string TimeZoneId = "America/Tijuana",
    bool IncludeAlreadyNotified = false) : IRequest<Result<BirthdayBenefitNotificationPreviewDto>>;
