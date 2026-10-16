using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.SelfServiceSignup;

public sealed class SelfServiceSignupHandler(ISelfServiceSignupService signup)
    : IRequestHandler<SelfServiceSignupCommand, Result<SelfServiceSignupResult>>
{
    public Task<Result<SelfServiceSignupResult>> Handle(
        SelfServiceSignupCommand command,
        CancellationToken cancellationToken) =>
        signup.SignupAsync(command, cancellationToken);
}
