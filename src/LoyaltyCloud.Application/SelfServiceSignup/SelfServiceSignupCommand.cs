using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.SelfServiceSignup;

public sealed record SelfServiceSignupCommand(
    string Email,
    string Password,
    string BusinessDisplayName,
    string Slug,
    string? TimeZoneId,
    string SignupAttemptId) : IRequest<Result<SelfServiceSignupResult>>;

public sealed record SelfServiceSignupResult(
    Guid TenantId,
    string TenantSlug,
    Guid AdminUserId,
    DateTime TrialEndsAtUtc);
