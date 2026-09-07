using Microsoft.AspNetCore.Authorization;

namespace LoyaltyCloud.Admin.Auth;

public static class TenantUserRoles
{
    public const string Admin = "Admin";
    public const string Cashier = "Cashier";
}

public static class TenantAuthorizationPolicies
{
    public const string TenantUser = "TenantUser";
    public const string TenantAdmin = "TenantAdmin";
    public const string CashierOperations = "CashierOperations";
}

public static class TenantAuthorization
{
    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(TenantAuthorizationPolicies.TenantUser, policy =>
            policy.RequireAuthenticatedUser()
                .RequireRole(TenantUserRoles.Admin, TenantUserRoles.Cashier));
        options.AddPolicy(TenantAuthorizationPolicies.TenantAdmin, policy =>
            policy.RequireAuthenticatedUser()
                .RequireRole(TenantUserRoles.Admin));
        options.AddPolicy(TenantAuthorizationPolicies.CashierOperations, policy =>
            policy.RequireAuthenticatedUser()
                .RequireRole(TenantUserRoles.Admin, TenantUserRoles.Cashier));
        options.AddPolicy(GiftCardsAuthorization.Policy, policy =>
            policy.RequireAuthenticatedUser()
                .RequireRole(TenantUserRoles.Admin)
                .AddRequirements(new GiftCardsEnabledRequirement()));

        options.DefaultPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireRole(TenantUserRoles.Admin)
            .Build();
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireRole(TenantUserRoles.Admin)
            .Build();
    }
}
