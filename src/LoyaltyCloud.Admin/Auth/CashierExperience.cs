using System.Security.Claims;

namespace LoyaltyCloud.Admin.Auth;

public static class CashierExperience
{
    public static bool IsEmbeddedAdmin(ClaimsPrincipal principal) =>
        principal.Identity?.IsAuthenticated == true && principal.IsInRole(TenantUserRoles.Admin)
        && !principal.IsInRole(TenantUserRoles.Cashier);
}
