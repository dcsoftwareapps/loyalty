using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace LoyaltyCloud.API.Auth;

public sealed class CashierBearerAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string BearerPrefix = "Bearer ";
    private readonly CashierAccessTokenService _tokens;

    public CashierBearerAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        CashierAccessTokenService tokens) : base(options, logger, encoder)
    {
        _tokens = tokens;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization))
            return Task.FromResult(AuthenticateResult.NoResult());

        if (!authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        try
        {
            var validation = _tokens.ValidateToken(authorization[BearerPrefix.Length..].Trim());
            if (!validation.Succeeded || validation.Principal is null)
                return Task.FromResult(AuthenticateResult.Fail(validation.FailureReason ?? "invalid_token"));

            var ticket = new AuthenticationTicket(validation.Principal, CashierAuthDefaults.AuthenticationScheme);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogError(ex, "Cashier bearer authentication failed because CashierAuth is not configured.");
            return Task.FromResult(AuthenticateResult.Fail("cashier_auth_not_configured"));
        }
    }
}
