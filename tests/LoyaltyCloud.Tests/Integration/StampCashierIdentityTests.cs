extern alias AdminApp;

using System.Security.Claims;
using AdminApp::LoyaltyCloud.Admin.Auth;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

[Trait("Category", "StampCashier")]
public sealed class StampCashierIdentityTests
{
    [Fact]
    public void Circuit_identity_does_not_require_http_context_and_anonymous_state_clears_identity()
    {
        var current = new AdminCurrentUserService(new HttpContextAccessor());
        var id = Guid.NewGuid().ToString();
        Assert.False(current.IsAuthenticated);
        current.SetCircuitPrincipal(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", id), new Claim("name", "cashier")], "Cookies", "name", ClaimTypes.Role)));
        Assert.True(current.IsAuthenticated);
        Assert.Equal(id, current.UserId);
        current.SetCircuitPrincipal(new ClaimsPrincipal(new ClaimsIdentity()));
        Assert.False(current.IsAuthenticated);
        Assert.Null(current.UserId);
    }
}
