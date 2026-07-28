using LoyaltyCloud.Application.Provisioning;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Tests.Integration;

internal static class IntegrationTestSeed
{
    public static async Task EnsureKBeautyPlatformRowsAsync(AppDbContext db)
    {
        if (await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == TenantSeed.KBeautyTenantId))
            return;

        var now = DateTime.UtcNow;
        db.Tenants.Add(new Tenant(
            TenantSeed.KBeautyTenantId,
            TenantSeed.KBeautySlug,
            "KBeauty",
            "America/Tijuana",
            now));
        db.TenantBrandings.Add(new TenantBranding(
            TenantSeed.KBeautyTenantId,
            primaryColor: "#1C1C1C",
            secondaryColor: "#E8668E"));
        db.TenantSubscriptions.Add(new TenantSubscription(
            TenantSeed.KBeautyTenantId,
            TenantSubscriptionStatus.Active,
            "internal",
            paidThroughUtc: DateTime.UtcNow.AddDays(30)));

        foreach (var row in TenantProvisioningDefaults.ProgramConfigRows)
        {
            db.ProgramConfigs.Add(new ProgramConfig(
                Guid.NewGuid(),
                TenantSeed.KBeautyTenantId,
                row.Key,
                row.Value,
                now,
                row.Description,
                TenantProvisioningDefaults.UpdatedBy));
        }

        await db.SaveChangesAsync();
    }

    public static async Task EnsureDefaultTenantLevelsAsync(AppDbContext db)
    {
        await EnsureDefaultTenantLevelsAsync(db, TenantSeed.KBeautyTenantId);
    }

    public static async Task EnsureProgramConfigAsync(AppDbContext db, Guid tenantId)
    {
        if (await db.ProgramConfigs.AnyAsync(config => config.TenantId == tenantId))
            return;

        var now = DateTime.UtcNow;
        foreach (var row in TenantProvisioningDefaults.ProgramConfigRows)
        {
            db.ProgramConfigs.Add(new ProgramConfig(
                Guid.NewGuid(),
                tenantId,
                row.Key,
                row.Value,
                now,
                row.Description,
                TenantProvisioningDefaults.UpdatedBy));
        }

        await db.SaveChangesAsync();
    }

    public static async Task EnsureDefaultTenantLevelsAsync(AppDbContext db, Guid tenantId)
    {
        if (await db.TenantLoyaltyLevels.AnyAsync(level => level.TenantId == tenantId))
            return;

        var now = DateTime.UtcNow;
        db.TenantLoyaltyLevels.AddRange(
            new TenantLoyaltyLevel(
                Guid.NewGuid(),
                tenantId,
                "Mist",
                0,
                1,
                now),
            new TenantLoyaltyLevel(
                Guid.NewGuid(),
                tenantId,
                "Glow",
                1000,
                2,
                now),
            new TenantLoyaltyLevel(
                Guid.NewGuid(),
                tenantId,
                "Radiance",
                3000,
                3,
                now));
        await db.SaveChangesAsync();
    }
}
