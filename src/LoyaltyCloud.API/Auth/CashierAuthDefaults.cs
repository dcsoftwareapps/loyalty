using System.Security.Claims;

namespace LoyaltyCloud.API.Auth;

public static class CashierAuthDefaults
{
    public const string AuthenticationScheme = "LoyaltyCloud.CashierBearer";
    public const string LoginRateLimitPolicy = "CashierLogin";
    public const string OperatorIdHeader = "X-Operator-Id";
    public const string AdminRole = "Admin";
    public const string CashierRole = "Cashier";

    public static readonly string[] AllowedRoles = [AdminRole, CashierRole];
}

public static class CashierClaimTypes
{
    public const string Subject = "sub";
    public const string TenantId = "tenant_id";
    public const string TenantSlug = "tenant_slug";
    public const string Name = "name";
    public const string AuthTime = "auth_time";
    public const string Issuer = "iss";
    public const string Audience = "aud";
    public const string ExpiresAt = "exp";
    public const string IssuedAt = "iat";
    public const string Role = ClaimTypes.Role;
}
