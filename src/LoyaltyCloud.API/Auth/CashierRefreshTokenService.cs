using System.Security.Cryptography;
using System.Text;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LoyaltyCloud.API.Auth;

public sealed class CashierRefreshTokenService(AppDbContext db, IMutableTenantContext context,
    IDateTimeProvider clock, IOptions<CashierAuthOptions> options)
{
    public (string Token, DateTimeOffset Expires) Issue(Tenant tenant, TenantAdminUser user)
    {
        var raw=WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var expires=clock.UtcNow.AddDays(Math.Max(1,options.Value.RememberMeRefreshTokenDays));
        db.Add(new CashierRefreshToken(tenant.Id,user.Id,Hash(raw),clock.UtcNow,expires));
        return (raw,new DateTimeOffset(DateTime.SpecifyKind(expires,DateTimeKind.Utc)));
    }

    public async Task<(Tenant Tenant,TenantAdminUser User,string Token,DateTimeOffset Expires)?> RotateAsync(string raw,CancellationToken ct)
    {
        var stored=await FindAsync(raw,ct);
        if (stored is null || !stored.IsUsable(clock.UtcNow)) return null;
        var tenant=await db.Tenants.AsNoTracking().Include(t=>t.Subscription).SingleOrDefaultAsync(t=>t.Id==stored.TenantId,ct);
        if (tenant is null || !tenant.IsActive || tenant.Subscription is null || !tenant.Subscription.IsOperational(clock.UtcNow)) return null;
        context.SetTenant(tenant.Id,tenant.Slug);
        var user=await db.TenantAdminUsers.AsNoTracking().SingleOrDefaultAsync(u=>u.Id==stored.TenantAdminUserId && u.TenantId==tenant.Id,ct);
        if (user is null || !user.IsActive || !CashierAuthDefaults.AllowedRoles.Contains(user.Role.ToString())) return null;
        stored.Revoke(clock.UtcNow);
        var issued=Issue(tenant,user);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return null; }
        // SaveChanges atomically revokes the old row and inserts its successor.
        return (tenant,user,issued.Token,issued.Expires);
    }

    public async Task RevokeAsync(string raw,CancellationToken ct)
    {
        var stored=await FindAsync(raw,ct);
        if (stored is null || stored.RevokedAtUtc is not null) return;
        var tenant=await db.Tenants.AsNoTracking().SingleOrDefaultAsync(t=>t.Id==stored.TenantId,ct);
        if (tenant is null) return;
        context.SetTenant(tenant.Id,tenant.Slug); stored.Revoke(clock.UtcNow);
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); }
    }

    // Future password change/reset can call this in the user's resolved tenant scope.
    public async Task RevokeAllForUserAsync(Guid userId,CancellationToken ct=default)
    {
        var tokens=await db.CashierRefreshTokens.Where(t=>t.TenantAdminUserId==userId && t.RevokedAtUtc==null).ToListAsync(ct);
        foreach(var token in tokens) token.Revoke(clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }
    private Task<CashierRefreshToken?> FindAsync(string raw,CancellationToken ct) =>
        string.IsNullOrWhiteSpace(raw) || raw.Length!=43 ? Task.FromResult<CashierRefreshToken?>(null)
        : db.CashierRefreshTokens.IgnoreQueryFilters().SingleOrDefaultAsync(t=>t.TokenHash==Hash(raw),ct);
    public static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
