using Microsoft.AspNetCore.Authorization;

namespace LoyaltyCloud.API.Auth;

public static class CashierAuthorizationPolicies
{
    public const string CashierOperations = "CashierOperations";
    public const string TenantAdminApi = "TenantAdminApi";

    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(CashierOperations, policy =>
            policy.RequireAuthenticatedUser()
                .RequireRole(CashierAuthDefaults.AdminRole, CashierAuthDefaults.CashierRole));

        options.AddPolicy(TenantAdminApi, policy =>
            policy.RequireAuthenticatedUser()
                .RequireRole(CashierAuthDefaults.AdminRole));
    }
}
