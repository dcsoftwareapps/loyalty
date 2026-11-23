using System.Security.Claims;
using LoyaltyCloud.Common.Services;

namespace LoyaltyCloud.Admin.Auth;

/// <summary>HTTP identity by default; authenticated circuit identity for server-side Stamps operations.</summary>
public sealed class AdminCurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private ClaimsPrincipal? circuitUser;
    private ClaimsPrincipal? User => circuitUser ?? accessor.HttpContext?.User;
    public string? UserId => User?.FindFirstValue("sub") ?? User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? User?.Identity?.Name;
    public string? UserName => User?.Identity?.Name ?? User?.FindFirstValue("name");
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
    public void SetCircuitPrincipal(ClaimsPrincipal principal) => circuitUser = principal;
}
