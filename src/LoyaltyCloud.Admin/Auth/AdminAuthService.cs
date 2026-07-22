using System.Globalization;
using System.Security.Claims;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace LoyaltyCloud.Admin.Auth;

/// <summary>
/// Autenticacion tenant-aware para el Admin. El tenant viene del slug de la ruta
/// de login, nunca de un campo editable ni de headers.
/// </summary>
public sealed class AdminAuthService
{
    private readonly ITenantRepository _tenants;
    private readonly ITenantAdminUserRepository _adminUsers;
    private readonly IMutableTenantContext _tenantContext;
    private readonly IPasswordHashingService _passwords;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;
    private readonly AdminAuthOptions _options;
    private readonly ILogger<AdminAuthService> _logger;

    public AdminAuthService(
        ITenantRepository tenants,
        ITenantAdminUserRepository adminUsers,
        IMutableTenantContext tenantContext,
        IPasswordHashingService passwords,
        IDateTimeProvider clock,
        IUnitOfWork uow,
        IOptions<AdminAuthOptions> options,
        ILogger<AdminAuthService> logger)
    {
        _tenants = tenants;
        _adminUsers = adminUsers;
        _tenantContext = tenantContext;
        _passwords = passwords;
        _clock = clock;
        _uow = uow;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AdminLoginResult> TrySignInAsync(
        HttpContext context,
        string tenantSlug,
        string? username,
        string? password,
        CancellationToken ct = default)
    {
        var tenant = await ResolveTenantAsync(tenantSlug, ct);
        if (tenant is null)
        {
            _logger.LogWarning("Admin login failed. TenantSlug={TenantSlug}, Reason={Reason}", tenantSlug, "tenant_not_found");
            return AdminLoginResult.TenantNotFound;
        }

        if (!IsOperational(tenant))
        {
            _logger.LogWarning("Admin login failed. TenantSlug={TenantSlug}, Reason={Reason}", tenant.Slug, "tenant_not_operational");
            return AdminLoginResult.TenantUnavailable;
        }

        _tenantContext.SetTenant(tenant.Id, tenant.Slug);

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("Admin login failed. TenantSlug={TenantSlug}, Reason={Reason}", tenant.Slug, "missing_credentials");
            return AdminLoginResult.InvalidCredentials;
        }

        var adminUser = await _adminUsers.GetByUsernameAsync(tenant.Id, username.Trim(), ct);
        if (adminUser is null
            || !adminUser.IsActive
            || !_passwords.VerifyPassword(adminUser.PasswordHash, password))
        {
            _logger.LogWarning("Admin login failed. TenantSlug={TenantSlug}, Reason={Reason}", tenant.Slug, "invalid_credentials");
            return AdminLoginResult.InvalidCredentials;
        }

        adminUser.RecordLogin(_clock.UtcNow);
        _adminUsers.Update(adminUser);
        await _uow.SaveChangesAsync(ct);

        await SignInAsync(context, tenant, adminUser);
        _logger.LogInformation(
            "Admin login succeeded. TenantId={TenantId}, TenantSlug={TenantSlug}, AdminUserId={AdminUserId}",
            tenant.Id,
            tenant.Slug,
            adminUser.Id);

        return AdminLoginResult.Success;
    }

    public async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var tenantIdRaw = context.Principal?.FindFirstValue(AdminClaimTypes.TenantId);
        var tenantSlug = context.Principal?.FindFirstValue(AdminClaimTypes.TenantSlug);
        var adminUserIdRaw = context.Principal?.FindFirstValue(AdminClaimTypes.Subject);

        if (!Guid.TryParse(tenantIdRaw, out var tenantId)
            || !Guid.TryParse(adminUserIdRaw, out var adminUserId)
            || string.IsNullOrWhiteSpace(tenantSlug))
        {
            Reject(context, tenantIdRaw, adminUserIdRaw, "invalid_claims");
            return;
        }

        Tenant? tenant;
        try
        {
            tenant = await _tenants.GetByIdAsync(tenantId, context.HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Admin principal rejected. TenantId={TenantId}, AdminUserId={AdminUserId}, Reason={Reason}", tenantId, adminUserId, "tenant_lookup_failed");
            context.RejectPrincipal();
            return;
        }

        if (tenant is null || !string.Equals(tenant.Slug, tenantSlug, StringComparison.Ordinal))
        {
            Reject(context, tenantId, adminUserId, "tenant_mismatch");
            return;
        }

        if (!IsOperational(tenant))
        {
            Reject(context, tenantId, adminUserId, "tenant_not_operational");
            return;
        }

        _tenantContext.SetTenant(tenant.Id, tenant.Slug);
        var adminUser = await _adminUsers.GetByIdAsync(adminUserId, context.HttpContext.RequestAborted);
        if (adminUser is null || adminUser.TenantId != tenant.Id || !adminUser.IsActive)
        {
            Reject(context, tenantId, adminUserId, "admin_inactive_or_missing");
            return;
        }
    }

    public async Task<bool> TrySetTenantContextFromPrincipalAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return false;

        var tenantIdRaw = context.User.FindFirstValue(AdminClaimTypes.TenantId);
        var tenantSlug = context.User.FindFirstValue(AdminClaimTypes.TenantSlug);
        var adminUserIdRaw = context.User.FindFirstValue(AdminClaimTypes.Subject);

        if (!Guid.TryParse(tenantIdRaw, out var tenantId)
            || string.IsNullOrWhiteSpace(tenantSlug))
        {
            _logger.LogWarning(
                "Admin principal rejected. TenantId={TenantId}, AdminUserId={AdminUserId}, Reason={Reason}",
                tenantIdRaw ?? "<null>",
                adminUserIdRaw ?? "<null>",
                "invalid_claims");
            await SignOutAsync(context);
            return false;
        }

        var tenant = await _tenants.GetByIdAsync(tenantId, context.RequestAborted);
        if (tenant is null || !string.Equals(tenant.Slug, tenantSlug, StringComparison.Ordinal) || !IsOperational(tenant))
        {
            _logger.LogWarning(
                "Admin principal rejected. TenantId={TenantId}, AdminUserId={AdminUserId}, Reason={Reason}",
                tenantId,
                adminUserIdRaw ?? "<null>",
                tenant is null ? "tenant_not_found" : "tenant_not_operational_or_mismatch");
            await SignOutAsync(context);
            return false;
        }

        _tenantContext.SetTenant(tenant.Id, tenant.Slug);
        return true;
    }

    public async Task SignOutAsync(HttpContext context) =>
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    public string GetLoginPathForCurrentPrincipal(HttpContext context)
    {
        var tenantSlug = context.User.FindFirstValue(AdminClaimTypes.TenantSlug);
        return string.IsNullOrWhiteSpace(tenantSlug)
            ? "/kbeauty/login"
            : $"/{tenantSlug}/login";
    }

    private async Task SignInAsync(HttpContext context, Tenant tenant, TenantAdminUser adminUser)
    {
        var authTime = new DateTimeOffset(_clock.UtcNow).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var claims = new List<Claim>
        {
            new(AdminClaimTypes.Subject, adminUser.Id.ToString()),
            new(AdminClaimTypes.TenantId, tenant.Id.ToString()),
            new(AdminClaimTypes.TenantSlug, tenant.Slug),
            new(AdminClaimTypes.Name, adminUser.Username),
            new(AdminClaimTypes.AuthTime, authTime)
        };
        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme,
            AdminClaimTypes.Name,
            roleType: "role");
        var principal = new ClaimsPrincipal(identity);

        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(Math.Max(1, _options.SessionHours))
            });
    }

    private async Task<Tenant?> ResolveTenantAsync(string tenantSlug, CancellationToken ct)
    {
        try
        {
            return await _tenants.GetBySlugAsync(tenantSlug, ct);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private bool IsOperational(Tenant tenant) =>
        tenant.IsActive
        && tenant.Subscription is not null
        && tenant.Subscription.IsOperational(_clock.UtcNow);

    private void Reject(CookieValidatePrincipalContext context, object? tenantId, object? adminUserId, string reason)
    {
        _logger.LogWarning(
            "Admin principal rejected. TenantId={TenantId}, AdminUserId={AdminUserId}, Reason={Reason}",
            tenantId ?? "<null>",
            adminUserId ?? "<null>",
            reason);
        context.RejectPrincipal();
    }
}

public enum AdminLoginResult
{
    Success,
    InvalidCredentials,
    TenantNotFound,
    TenantUnavailable
}
