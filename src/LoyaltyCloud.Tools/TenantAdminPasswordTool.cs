using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Tools;

public sealed class TenantAdminPasswordTool
{
    private readonly AppDbContext _db;
    private readonly IMutableTenantContext _tenantContext;
    private readonly IPasswordHashingService _passwords;

    public TenantAdminPasswordTool(
        AppDbContext db,
        IMutableTenantContext tenantContext,
        IPasswordHashingService passwords)
    {
        _db = db;
        _tenantContext = tenantContext;
        _passwords = passwords;
    }

    public async Task<int> ResetPasswordAsync(
        string? tenantSlug,
        string? adminUsername,
        string? passwordFromEnvironment,
        TextWriter output,
        TextWriter error,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(passwordFromEnvironment))
        {
            error.WriteLine("Falta LOYALTYCLOUD_ADMIN_PASSWORD.");
            return 2;
        }

        var tenant = await ResolveTenantAsync(tenantSlug, error, ct);
        if (tenant is null)
            return 1;

        _tenantContext.SetTenant(tenant.Id, tenant.Slug);

        TenantAdminUser? adminUser;
        try
        {
            var normalizedUsername = TenantAdminUser.NormalizeUsername(adminUsername ?? string.Empty);
            adminUser = await _db.TenantAdminUsers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.TenantId == tenant.Id && u.NormalizedUsername == normalizedUsername, ct);
        }
        catch (ArgumentException)
        {
            error.WriteLine("Falta --admin-username.");
            return 2;
        }

        if (adminUser is null)
        {
            error.WriteLine($"Admin no encontrado. TenantSlug={tenant.Slug}; Username={adminUsername}");
            return 1;
        }

        adminUser.ChangePasswordHash(_passwords.HashPassword(passwordFromEnvironment));
        await _db.SaveChangesAsync(ct);

        output.WriteLine($"Password reset completed. TenantSlug={tenant.Slug}; Username={adminUser.Username}");
        return 0;
    }

    public async Task<int> ListAdminsAsync(
        string? tenantSlug,
        TextWriter output,
        TextWriter error,
        CancellationToken ct = default)
    {
        var tenant = await ResolveTenantAsync(tenantSlug, error, ct);
        if (tenant is null)
            return 1;

        _tenantContext.SetTenant(tenant.Id, tenant.Slug);

        var admins = await _db.TenantAdminUsers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.TenantId == tenant.Id)
            .OrderBy(u => u.Username)
            .Select(u => new { u.Username, u.IsActive })
            .ToListAsync(ct);

        output.WriteLine("Username\tIsActive");
        foreach (var admin in admins)
            output.WriteLine($"{admin.Username}\t{admin.IsActive}");

        return 0;
    }

    private async Task<Tenant?> ResolveTenantAsync(string? tenantSlug, TextWriter error, CancellationToken ct)
    {
        string normalizedSlug;
        try
        {
            normalizedSlug = Tenant.NormalizeSlug(tenantSlug ?? string.Empty);
        }
        catch (ArgumentException)
        {
            error.WriteLine("Falta --tenant-slug.");
            return null;
        }

        var tenant = await _db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == normalizedSlug, ct);

        if (tenant is null)
            error.WriteLine($"Tenant no encontrado. TenantSlug={normalizedSlug}");

        return tenant;
    }
}
