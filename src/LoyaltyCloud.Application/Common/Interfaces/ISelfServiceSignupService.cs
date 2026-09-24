using LoyaltyCloud.Application.SelfServiceSignup;
using LoyaltyCloud.Common.Results;

namespace LoyaltyCloud.Application.Common.Interfaces;

public interface ISelfServiceSignupService
{
    Task<Result<SelfServiceSignupResult>> SignupAsync(
        SelfServiceSignupCommand command,
        CancellationToken cancellationToken = default);
}
