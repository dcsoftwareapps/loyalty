using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoyaltyCloud.Infrastructure.Services.GoogleWallet;

internal sealed class GoogleWalletBrandingSynchronizer(
    IDbContextFactory<AppDbContext> factory,
    ITenantContext tenant,
    ITenantWalletBrandingReadService branding,
    IGoogleWalletClient client,
    GoogleWalletObjectMapper mapper,
    IOptions<GoogleWalletOptions> options,
    ILogger<GoogleWalletBrandingSynchronizer> logger)
{
    public async Task<bool> RefreshAsync(Guid tenantId, CancellationToken ct)
    {
        if (tenantId != tenant.RequireTenantId()) throw new InvalidOperationException("Tenant inválido.");
        if (!options.Value.Enabled) return true;
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var classes = await db.MemberDigitalWallets.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.Provider == DigitalWalletProvider.Google)
                .Select(x => x.ExternalClassId).Distinct().ToListAsync(ct);
            if (classes.Count == 0) return true;
            var effective = await branding.GetForTenantAsync(tenantId, ct);
            var success = true;
            foreach (var classId in classes)
            {
                // Never patch a legacy class still referenced by another tenant.
                var shared = await db.MemberDigitalWallets.IgnoreQueryFilters().AsNoTracking()
                    .AnyAsync(x => x.Provider == DigitalWalletProvider.Google
                        && x.ExternalClassId == classId && x.TenantId != tenantId, ct);
                if (shared) { success = false; logger.LogWarning("Shared legacy Loyalty class skipped. TenantId={TenantId}", tenantId); continue; }
                try { await client.EnsureLoyaltyClassAsync(mapper.ToClassData(classId, options.Value, effective), ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) { success = false; logger.LogWarning("Loyalty branding sync failed. TenantId={TenantId}, FailureType={FailureType}", tenantId, ex.GetType().Name); }
            }
            return success;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning("Loyalty branding sync unavailable. TenantId={TenantId}, FailureType={FailureType}", tenantId, ex.GetType().Name);
            return false;
        }
    }
}
