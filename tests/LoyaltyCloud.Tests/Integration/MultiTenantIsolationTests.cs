using LoyaltyCloud.Application;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Customers.Queries.GetCustomerBySerial;
using LoyaltyCloud.Application.Levels.Commands.RecalculateLevels;
using LoyaltyCloud.Application.Points.Commands.AddPoints;
using LoyaltyCloud.Application.Redemptions.Commands.RedeemReward;
using LoyaltyCloud.Application.Redemptions.Queries.GetRedemptionCatalog;
using LoyaltyCloud.Application.Rewards.Commands.CreateReward;
using LoyaltyCloud.Common.Constants;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Domain.ValueObjects;
using LoyaltyCloud.Infrastructure;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class MultiTenantIsolationTests
{
    private static readonly Guid BellaTenantId = Guid.Parse("b2000000-0000-0000-0000-000000000001");
    private const string BellaSlug = "bella-salon";
    private const string KBeautySerial = "KB-TEST-001";
    private const string BellaSerial = "BS-TEST-001";
    private const string SharedPhone = "6461234567";
    private const string PassType = "pass.com.kbeautymx.loyalty";
    private const string SharedDevice = "mt2h-device";

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LoyaltyCloud.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("No se encontro LoyaltyCloud.sln.");
    }

    [Fact]
    [Trait("Category", "MultiTenant")]
    public async Task Customers_are_filtered_by_current_tenant()
    {
        await using var env = await MultiTenantTestEnvironment.CreateAsync();

        var kbeautyNames = await env.ReadAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, db =>
            db.Customers.OrderBy(c => c.FullName).Select(c => c.FullName).ToListAsync());
        var bellaNames = await env.ReadAsync(BellaTenantId, BellaSlug, db =>
            db.Customers.OrderBy(c => c.FullName).Select(c => c.FullName).ToListAsync());

        Assert.Contains("KBeauty Isolation Customer", kbeautyNames);
        Assert.DoesNotContain("Bella Isolation Customer", kbeautyNames);
        Assert.Contains("Bella Isolation Customer", bellaNames);
        Assert.DoesNotContain("KBeauty Isolation Customer", bellaNames);
    }

    [Fact]
    [Trait("Category", "MultiTenant")]
    public async Task Same_phone_is_allowed_across_tenants_but_rejected_within_same_tenant()
    {
        await using var env = await MultiTenantTestEnvironment.CreateAsync();

        var kbeauty = await env.ReadAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, db =>
            db.Customers.SingleAsync(c => c.NormalizedPhone == SharedPhone));
        var bella = await env.ReadAsync(BellaTenantId, BellaSlug, db =>
            db.Customers.SingleAsync(c => c.NormalizedPhone == SharedPhone));

        Assert.Equal(TenantSeed.KBeautyTenantId, kbeauty.TenantId);
        Assert.Equal(BellaTenantId, bella.TenantId);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            env.WriteAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, async db =>
            {
                db.Customers.Add(new Customer(
                    Guid.NewGuid(),
                    TenantSeed.KBeautyTenantId,
                    "Duplicate Phone",
                    "duplicate.phone@kbeauty.local",
                    new DateTime(1990, 1, 1),
                    DateTime.UtcNow,
                    SharedPhone));
                await db.SaveChangesAsync();
            }));
    }

    [Fact]
    [Trait("Category", "MultiTenant")]
    public async Task Rewards_are_tenant_isolated()
    {
        await using var env = await MultiTenantTestEnvironment.CreateAsync();

        var kbeautyRewards = await env.WithScopeAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, async sp =>
        {
            var rewards = await sp.GetRequiredService<IRewardCatalogRepository>().GetAllAsync();
            return rewards.Select(r => r.Name).ToArray();
        });
        var bellaRewards = await env.WithScopeAsync(BellaTenantId, BellaSlug, async sp =>
        {
            var rewards = await sp.GetRequiredService<IRewardCatalogRepository>().GetAllAsync();
            return rewards.Select(r => r.Name).ToArray();
        });

        Assert.Contains("KBeauty Reward", kbeautyRewards);
        Assert.DoesNotContain("Bella Reward", kbeautyRewards);
        Assert.Contains("Bella Reward", bellaRewards);
        Assert.DoesNotContain("KBeauty Reward", bellaRewards);
    }

    [Fact]
    [Trait("Category", "MultiTenant")]
    public async Task ProgramConfig_keys_are_tenant_isolated()
    {
        await using var env = await MultiTenantTestEnvironment.CreateAsync();

        var kbeautyValue = await env.WithScopeAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, async sp =>
            (await sp.GetRequiredService<IProgramConfigRepository>().GetByKeyAsync(LoyaltyConstants.ConfigKeys.PointsPerPesoUnit))!.Value);
        var bellaValue = await env.WithScopeAsync(BellaTenantId, BellaSlug, async sp =>
            (await sp.GetRequiredService<IProgramConfigRepository>().GetByKeyAsync(LoyaltyConstants.ConfigKeys.PointsPerPesoUnit))!.Value);

        Assert.Equal("10", kbeautyValue);
        Assert.Equal("20", bellaValue);
    }

    [Fact]
    [Trait("Category", "MultiTenant")]
    [Trait("Category", "MTLevel1")]
    public async Task Tenant_loyalty_level_read_service_is_tenant_isolated_and_ordered()
    {
        await using var env = await MultiTenantTestEnvironment.CreateAsync();
        await env.SeedBellaLoyaltyLevelsAsync();

        var kbeautyLevels = await env.WithScopeAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, async sp =>
            await sp.GetRequiredService<ITenantLoyaltyLevelReadService>().GetActiveLevelsAsync());
        var bellaLevels = await env.WithScopeAsync(BellaTenantId, BellaSlug, async sp =>
            await sp.GetRequiredService<ITenantLoyaltyLevelReadService>().GetActiveLevelsAsync());

        Assert.Equal(
            [LoyaltyConstants.Levels.Mist, LoyaltyConstants.Levels.Glow, LoyaltyConstants.Levels.Radiance],
            kbeautyLevels.Select(level => level.Name).ToArray());
        Assert.Equal([0, 1000, 3000], kbeautyLevels.Select(level => level.Threshold).ToArray());
        Assert.Equal([1, 2, 3], kbeautyLevels.Select(level => level.SortOrder).ToArray());

        Assert.Equal(["Bronce", "Plata", "Oro"], bellaLevels.Select(level => level.Name).ToArray());
        Assert.Equal([0, 500, 1500], bellaLevels.Select(level => level.Threshold).ToArray());
        Assert.Equal([1, 2, 3], bellaLevels.Select(level => level.SortOrder).ToArray());
    }

    [Fact]
    [Trait("Category", "MTLevel2")]
    [Trait("Category", "MTLevel4")]
    public async Task Level_calculation_uses_active_tenant_levels_instead_of_program_config_thresholds()
    {
        await using var env = await MultiTenantTestEnvironment.CreateAsync();
        await env.SeedBellaLoyaltyLevelsAsync();

        var kbeautyLevel = await env.WithScopeAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, async sp =>
        {
            var tenantLevels = await sp.GetRequiredService<ITenantLoyaltyLevelReadService>().GetActiveLevelsAsync();
            return sp.GetRequiredService<ILevelCalculationService>().CalculateLevel(1499, tenantLevels);
        });
        var bellaLevel = await env.WithScopeAsync(BellaTenantId, BellaSlug, async sp =>
        {
            var tenantLevels = await sp.GetRequiredService<ITenantLoyaltyLevelReadService>().GetActiveLevelsAsync();
            return sp.GetRequiredService<ILevelCalculationService>().CalculateLevel(1499, tenantLevels);
        });

        Assert.Equal(LoyaltyConstants.Levels.Glow, kbeautyLevel.Name);
        Assert.Equal(1000, kbeautyLevel.MinPoints);
        Assert.Equal("Plata", bellaLevel.Name);
        Assert.Equal(500, bellaLevel.MinPoints);
    }

    [Fact]
    [Trait("Category", "MTLevel2")]
    [Trait("Category", "MTLevel4")]
    public async Task Level_calculation_fails_explicitly_when_tenant_has_no_active_levels()
    {
        await using var env = await MultiTenantTestEnvironment.CreateAsync();
        var tenantId = Guid.Parse("b3000000-0000-0000-0000-000000000001");
        const string slug = "empty-levels";

        await env.PlatformWriteAsync(async db =>
        {
            db.Tenants.Add(new Tenant(tenantId, slug, "Empty Levels", "America/Tijuana", DateTime.UtcNow));
            db.TenantBrandings.Add(new TenantBranding(tenantId, primaryColor: "#111111", secondaryColor: "#EEEEEE"));
            db.TenantSubscriptions.Add(new TenantSubscription(tenantId, TenantSubscriptionStatus.Active, "development"));
            await db.SaveChangesAsync();
        });

        var exception = await env.WithScopeAsync(tenantId, slug, async sp =>
        {
            var tenantLevels = await sp.GetRequiredService<ITenantLoyaltyLevelReadService>().GetActiveLevelsAsync();
            return Assert.Throws<InvalidOperationException>(() =>
                sp.GetRequiredService<ILevelCalculationService>().CalculateLevel(0, tenantLevels));
        });

        Assert.Contains("No hay niveles activos", exception.Message);
    }

    [Fact]
    [Trait("Category", "MTLevel2")]
    public async Task Recalculate_levels_uses_same_dynamic_level_engine_for_current_tenant()
    {
        await using var env = await MultiTenantTestEnvironment.CreateAsync();
        await env.SeedBellaLoyaltyLevelsAsync();

        await env.WriteAsync(BellaTenantId, BellaSlug, async db =>
        {
            var card = await db.LoyaltyCards.SingleAsync(c => c.SerialNumber == BellaSerial);
            db.PointTransactions.Add(new PointTransaction(
                Guid.Parse("b2000002-0000-0000-0000-000000000901"),
                BellaTenantId,
                card.Id,
                1200,
                TransactionType.Purchase,
                "MT-Level-2 rolling purchase.",
                DateTime.UtcNow,
                purchaseAmount: 24000m,
                createdBy: "test"));
            await db.SaveChangesAsync();
        });

        var result = await env.WithScopeAsync(BellaTenantId, BellaSlug, async sp =>
            await sp.GetRequiredService<ISender>().Send(new RecalculateLevelsCommand("test")));

        var level = await env.ReadAsync(BellaTenantId, BellaSlug, db =>
            db.LoyaltyCards.Where(c => c.SerialNumber == BellaSerial).Select(c => c.Level).SingleAsync());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.CardsChanged);
        Assert.Equal(1, result.Value.CardsUpgraded);
        Assert.Equal("Oro", level);
    }

    [Fact]
    [Trait("Category", "MTLevel3")]
    public void Rewards_admin_page_loads_dynamic_level_options()
    {
        var source = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "src",
            "LoyaltyCloud.Admin",
            "Pages",
            "Rewards.razor"));

        Assert.Contains("ITenantLoyaltyLevelReadService", source);
        Assert.Contains("Todos los niveles", source);
        Assert.Contains("foreach (var level in levelOptions)", source);
        Assert.DoesNotContain("<option value=\"@LoyaltyConstants.Levels.Mist\">", source);
        Assert.DoesNotContain("<option value=\"@LoyaltyConstants.Levels.Glow\">", source);
        Assert.DoesNotContain("<option value=\"@LoyaltyConstants.Levels.Radiance\">", source);
    }

    [Fact]
    [Trait("Category", "MTLevel3")]
    public async Task Rewards_level_options_are_tenant_isolated()
    {
        await using var env = await MultiTenantTestEnvironment.CreateAsync();
        await env.SeedBellaLoyaltyLevelsAsync();

        var kbeautyLevels = await env.WithScopeAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, async sp =>
            await sp.GetRequiredService<ITenantLoyaltyLevelReadService>().GetActiveLevelsAsync());
        var bellaLevels = await env.WithScopeAsync(BellaTenantId, BellaSlug, async sp =>
            await sp.GetRequiredService<ITenantLoyaltyLevelReadService>().GetActiveLevelsAsync());

        Assert.Contains(kbeautyLevels, level => level.Name == LoyaltyConstants.Levels.Mist);
        Assert.DoesNotContain(kbeautyLevels, level => level.Name == "Oro");
        Assert.Contains(bellaLevels, level => level.Name == "Oro");
        Assert.DoesNotContain(bellaLevels, level => level.Name == LoyaltyConstants.Levels.Glow);
    }

    [Fact]
    [Trait("Category", "MTLevel3")]
    public async Task Redemption_catalog_uses_dynamic_reward_minimum_level()
    {
        await using var env = await MultiTenantTestEnvironment.CreateAsync();
        await env.SeedBellaLoyaltyLevelsAsync();
        await env.SeedBellaRewardsForDynamicLevelsAsync();

        var catalog = await env.WithScopeAsync(BellaTenantId, BellaSlug, async sp =>
            await sp.GetRequiredService<ISender>().Send(new GetRedemptionCatalogQuery(BellaSerial)));

        Assert.True(catalog.IsSuccess);
        Assert.Contains(catalog.Value, reward => reward.Name == "Todos reward");
        Assert.DoesNotContain(catalog.Value, reward => reward.Name == "Plata reward");
        Assert.DoesNotContain(catalog.Value, reward => reward.Name == "Oro reward");
    }

    [Fact]
    [Trait("Category", "MTLevel3")]
    public async Task Redemption_catalog_accepts_same_and_higher_dynamic_levels_with_fourth_and_fifth_levels()
    {
        await using var env = await MultiTenantTestEnvironment.CreateAsync();
        await env.SeedBellaLoyaltyLevelsAsync();
        await env.SeedBellaFourthAndFifthLevelsAsync();
        await env.SeedBellaRewardsForDynamicLevelsAsync(includeFourthAndFifth: true);
        await env.AddBellaRollingPointsAsync(3200);

        var catalog = await env.WithScopeAsync(BellaTenantId, BellaSlug, async sp =>
            await sp.GetRequiredService<ISender>().Send(new GetRedemptionCatalogQuery(BellaSerial)));

        Assert.True(catalog.IsSuccess);
        Assert.Contains(catalog.Value, reward => reward.Name == "Plata reward");
        Assert.Contains(catalog.Value, reward => reward.Name == "Oro reward");
        Assert.Contains(catalog.Value, reward => reward.Name == "Platino reward");
        Assert.DoesNotContain(catalog.Value, reward => reward.Name == "Diamante reward");
    }

    [Fact]
    [Trait("Category", "MTLevel3")]
    public async Task Create_reward_rejects_unknown_or_cross_tenant_minimum_level()
    {
        await using var env = await MultiTenantTestEnvironment.CreateAsync();
        await env.SeedBellaLoyaltyLevelsAsync();

        var unknown = await env.WithScopeAsync(BellaTenantId, BellaSlug, async sp =>
            await sp.GetRequiredService<ISender>().Send(new CreateRewardCommand(
                "Unknown level reward",
                "Test reward.",
                10,
                "NoExiste",
                false,
                null,
                null,
                true)));
        var crossTenant = await env.WithScopeAsync(BellaTenantId, BellaSlug, async sp =>
            await sp.GetRequiredService<ISender>().Send(new CreateRewardCommand(
                "Cross tenant level reward",
                "Test reward.",
                10,
                LoyaltyConstants.Levels.Glow,
                false,
                null,
                null,
                true)));

        Assert.True(unknown.IsFailure);
        Assert.Contains("no existe", unknown.Error, StringComparison.OrdinalIgnoreCase);
        Assert.True(crossTenant.IsFailure);
        Assert.Contains("tenant actual", crossTenant.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "MTLevel3")]
    public async Task Redeem_reward_revalidates_dynamic_level_server_side()
    {
        await using var env = await MultiTenantTestEnvironment.CreateAsync();
        await env.SeedBellaLoyaltyLevelsAsync();
        var rewardId = await env.SeedSingleBellaRewardAsync("Oro server-side reward", "Oro", pointsCost: 10);

        var catalog = await env.WithScopeAsync(BellaTenantId, BellaSlug, async sp =>
            await sp.GetRequiredService<ISender>().Send(new GetRedemptionCatalogQuery(BellaSerial)));
        var redeem = await env.WithScopeAsync(BellaTenantId, BellaSlug, async sp =>
            await sp.GetRequiredService<ISender>().Send(new RedeemRewardCommand(BellaSerial, rewardId, "test")));

        Assert.True(catalog.IsSuccess);
        Assert.DoesNotContain(catalog.Value, reward => reward.Id == rewardId);
        Assert.True(redeem.IsFailure);
        Assert.Contains("requiere Oro", redeem.Error);
    }

    [Fact]
    [Trait("Category", "MTLevel3")]
    public async Task Monthly_product_respects_dynamic_minimum_level()
    {
        await using var env = await MultiTenantTestEnvironment.CreateAsync();
        await env.SeedBellaLoyaltyLevelsAsync();
        var now = DateTime.UtcNow;
        await env.SeedSingleBellaRewardAsync(
            "Oro monthly product",
            "Oro",
            pointsCost: 10,
            isMonthlyProduct: true,
            validFrom: now.AddDays(-1),
            validTo: now.AddDays(1));

        var catalog = await env.WithScopeAsync(BellaTenantId, BellaSlug, async sp =>
            await sp.GetRequiredService<ISender>().Send(new GetRedemptionCatalogQuery(BellaSerial)));

        Assert.True(catalog.IsSuccess);
        Assert.DoesNotContain(catalog.Value, reward => reward.Name == "Oro monthly product");
    }

    [Fact]
    [Trait("Category", "MTLevel4")]
    public async Task Level_progress_supports_fourth_and_fifth_levels_without_hardcoded_names()
    {
        await using var env = await MultiTenantTestEnvironment.CreateAsync();
        await env.SeedBellaLoyaltyLevelsAsync();
        await env.SeedBellaFourthAndFifthLevelsAsync();

        var progress = await env.WithScopeAsync(BellaTenantId, BellaSlug, async sp =>
        {
            var levels = await sp.GetRequiredService<ITenantLoyaltyLevelReadService>().GetActiveLevelsAsync();
            return sp.GetRequiredService<ILevelProgressService>().Calculate(3200, levels);
        });
        var maxProgress = await env.WithScopeAsync(BellaTenantId, BellaSlug, async sp =>
        {
            var levels = await sp.GetRequiredService<ITenantLoyaltyLevelReadService>().GetActiveLevelsAsync();
            return sp.GetRequiredService<ILevelProgressService>().Calculate(5200, levels);
        });

        Assert.Equal("Platino", progress.CurrentLevel.Name);
        Assert.Equal(3000, progress.CurrentLevelThreshold);
        Assert.Equal("Diamante", progress.NextLevel?.Name);
        Assert.Equal(5000, progress.NextLevelThreshold);
        Assert.Equal(1800, progress.PointsToNextLevel);
        Assert.False(progress.IsMaxLevel);

        Assert.Equal("Diamante", maxProgress.CurrentLevel.Name);
        Assert.Null(maxProgress.NextLevel);
        Assert.Null(maxProgress.NextLevelThreshold);
        Assert.Equal(0, maxProgress.PointsToNextLevel);
        Assert.True(maxProgress.IsMaxLevel);
    }

    [Fact]
    [Trait("Category", "MTLevel4")]
    public async Task Customer_detail_uses_dynamic_rolling_progress_for_current_tenant()
    {
        await using var env = await MultiTenantTestEnvironment.CreateAsync();
        await env.SeedBellaLoyaltyLevelsAsync();
        await env.SeedBellaFourthAndFifthLevelsAsync();
        await env.AddBellaRollingPointsAsync(1250);

        var detail = await env.WithScopeAsync(BellaTenantId, BellaSlug, async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var customerId = await db.Customers
                .Where(customer => customer.Email == "isolation@bella.local")
                .Select(customer => customer.Id)
                .SingleAsync();
            return await sp.GetRequiredService<ICustomerDetailReadService>().GetByCustomerIdAsync(customerId);
        });

        Assert.NotNull(detail);
        Assert.Equal(1700, detail.LoyaltyAudit.RollingProgress.RollingPoints);
        Assert.Equal("Oro", detail.LoyaltyAudit.RollingProgress.CurrentLevel);
        Assert.Equal(1500, detail.LoyaltyAudit.RollingProgress.CurrentLevelThreshold);
        Assert.Equal("Platino", detail.LoyaltyAudit.RollingProgress.NextLevelName);
        Assert.Equal(3000, detail.LoyaltyAudit.RollingProgress.NextLevelThreshold);
        Assert.Equal(1300, detail.LoyaltyAudit.RollingProgress.PointsToNextLevel);
        Assert.False(detail.LoyaltyAudit.RollingProgress.IsMaxLevel);
    }

    [Fact]
    [Trait("Category", "MTLevel4")]
    public void Customer_detail_ui_does_not_render_fixed_glow_radiance_progress_labels()
    {
        var source = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "src",
            "LoyaltyCloud.Admin",
            "Pages",
            "CustomerDetail.razor"));

        Assert.Contains("Nivel actual", source);
        Assert.Contains("Siguiente nivel", source);
        Assert.Contains("Umbral siguiente", source);
        Assert.DoesNotContain("Label=\"Glow\"", source);
        Assert.DoesNotContain("Label=\"Radiance\"", source);
        Assert.DoesNotContain("GlowThreshold", source);
        Assert.DoesNotContain("RadianceThreshold", source);
    }

    [Fact]
    [Trait("Category", "MTLevel4")]
    public void Wallet_progress_generation_does_not_use_legacy_level_constants()
    {
        var source = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "src",
            "LoyaltyCloud.Infrastructure",
            "Services",
            "PassGeneratorService.cs"));

        Assert.Contains("ILevelProgressService", source);
        Assert.Contains("ITenantLoyaltyLevelReadService", source);
        Assert.Contains("GetEligibleLevelPointsAsync", source);
        Assert.DoesNotContain("LevelGlowMin", source);
        Assert.DoesNotContain("LevelRadianceMin", source);
        Assert.DoesNotContain("LoyaltyConstants.Levels.Glow", source);
        Assert.DoesNotContain("LoyaltyConstants.Levels.Radiance", source);
    }

    [Fact]
    [Trait("Category", "MTLevel4")]
    public void Level_badge_has_neutral_fallback_for_dynamic_level_names()
    {
        var component = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "src",
            "LoyaltyCloud.Admin",
            "Components",
            "LevelBadge.razor"));
        var styles = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "src",
            "LoyaltyCloud.Admin",
            "wwwroot",
            "css",
            "site.css"));

        Assert.Contains("kb-level--neutral", component);
        Assert.Contains(".kb-level--neutral", styles);
    }

    [Fact]
    [Trait("Category", "MTLevel3")]
    public void Rewards_and_redemptions_do_not_hardcode_legacy_level_ranking()
    {
        var files = new[]
        {
            Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Application", "Rewards"),
            Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Application", "Redemptions"),
            Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Infrastructure", "Repositories", "RewardCatalogRepository.cs")
        };

        var source = string.Join(Environment.NewLine, files.SelectMany(path =>
            Directory.Exists(path)
                ? Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText)
                : [File.ReadAllText(path)]));

        Assert.DoesNotContain("LevelMistMin", source);
        Assert.DoesNotContain("LevelGlowMin", source);
        Assert.DoesNotContain("LevelRadianceMin", source);
        Assert.DoesNotContain("LoyaltyConstants.Levels.Mist", source);
        Assert.DoesNotContain("LoyaltyConstants.Levels.Glow", source);
        Assert.DoesNotContain("LoyaltyConstants.Levels.Radiance", source);
    }

    [Fact]
    [Trait("Category", "MultiTenant")]
    [Trait("Category", "MTLevel1")]
    public async Task Existing_tenant_level_migration_uses_program_config_without_changing_loyalty_card_level()
    {
        await using var env = await MultiTenantTestEnvironment.CreateAsync();

        var row = await env.PlatformReadAsync(async db => new
        {
            Levels = await db.TenantLoyaltyLevels
                .IgnoreQueryFilters()
                .Where(level => level.TenantId == TenantSeed.KBeautyTenantId)
                .OrderBy(level => level.SortOrder)
                .Select(level => new { level.Name, level.Threshold, level.SortOrder })
                .ToListAsync(),
            CardLevel = await db.LoyaltyCards
                .IgnoreQueryFilters()
                .Where(card => card.TenantId == TenantSeed.KBeautyTenantId && card.SerialNumber == KBeautySerial)
                .Select(card => card.Level)
                .SingleAsync(),
            ProgramConfigThresholds = await db.ProgramConfigs
                .IgnoreQueryFilters()
                .Where(config => config.TenantId == TenantSeed.KBeautyTenantId
                              && (config.Key == LoyaltyConstants.ConfigKeys.LevelMistMin
                               || config.Key == LoyaltyConstants.ConfigKeys.LevelGlowMin
                               || config.Key == LoyaltyConstants.ConfigKeys.LevelRadianceMin))
                .ToDictionaryAsync(config => config.Key, config => config.Value)
        });

        Assert.Collection(
            row.Levels,
            level =>
            {
                Assert.Equal(LoyaltyConstants.Levels.Mist, level.Name);
                Assert.Equal(int.Parse(row.ProgramConfigThresholds[LoyaltyConstants.ConfigKeys.LevelMistMin]), level.Threshold);
                Assert.Equal(1, level.SortOrder);
            },
            level =>
            {
                Assert.Equal(LoyaltyConstants.Levels.Glow, level.Name);
                Assert.Equal(int.Parse(row.ProgramConfigThresholds[LoyaltyConstants.ConfigKeys.LevelGlowMin]), level.Threshold);
                Assert.Equal(2, level.SortOrder);
            },
            level =>
            {
                Assert.Equal(LoyaltyConstants.Levels.Radiance, level.Name);
                Assert.Equal(int.Parse(row.ProgramConfigThresholds[LoyaltyConstants.ConfigKeys.LevelRadianceMin]), level.Threshold);
                Assert.Equal(3, level.SortOrder);
            });
        Assert.Equal(LoyaltyConstants.Levels.Mist, row.CardLevel);
    }

    [Fact]
    [Trait("Category", "MultiTenant")]
    public async Task Cross_tenant_relationships_are_rejected_by_sql_constraints()
    {
        await using var env = await MultiTenantTestEnvironment.CreateAsync();
        var ids = await env.PlatformReadAsync(async db => new
            {
                KBeautyCustomerId = (await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.TenantId == TenantSeed.KBeautyTenantId)).Id,
                KBeautyCardId = (await db.LoyaltyCards.IgnoreQueryFilters().SingleAsync(c => c.TenantId == TenantSeed.KBeautyTenantId)).Id,
                BellaRewardId = (await db.RewardCatalogItems.IgnoreQueryFilters().SingleAsync(r => r.TenantId == BellaTenantId)).Id
            });

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            env.WriteAsync(BellaTenantId, BellaSlug, async db =>
            {
                db.LoyaltyCards.Add(new LoyaltyCard(
                    Guid.NewGuid(),
                    BellaTenantId,
                    ids.KBeautyCustomerId,
                    "BS-XTEN-001",
                    DateTime.UtcNow));
                await db.SaveChangesAsync();
            }));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            env.WriteAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, async db =>
            {
                db.Redemptions.Add(new Redemption(
                    Guid.NewGuid(),
                    TenantSeed.KBeautyTenantId,
                    ids.KBeautyCardId,
                    ids.BellaRewardId,
                    10,
                    DateTime.UtcNow));
                await db.SaveChangesAsync();
            }));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            env.WriteAsync(BellaTenantId, BellaSlug, async db =>
            {
                db.PointTransactions.Add(new PointTransaction(
                    Guid.NewGuid(),
                    BellaTenantId,
                    ids.KBeautyCardId,
                    5,
                    TransactionType.Purchase,
                    "Cross tenant transaction",
                    DateTime.UtcNow));
                await db.SaveChangesAsync();
            }));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            env.WriteAsync(BellaTenantId, BellaSlug, async db =>
            {
                db.DeviceRegistrations.Add(new DeviceRegistration(
                    Guid.NewGuid(),
                    BellaTenantId,
                    "cross-device",
                    PassType,
                    KBeautySerial,
                    "push-token-cross",
                    DateTime.UtcNow));
                await db.SaveChangesAsync();
            }));
    }

    [Fact]
    [Trait("Category", "MultiTenant")]
    public async Task Write_guard_rejects_wrong_tenant_and_tenant_id_mutation()
    {
        await using var env = await MultiTenantTestEnvironment.CreateAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            env.WriteAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, async db =>
            {
                db.Customers.Add(new Customer(
                    Guid.NewGuid(),
                    BellaTenantId,
                    "Wrong Tenant",
                    "wrong.tenant@kbeauty.local",
                    new DateTime(1990, 1, 1),
                    DateTime.UtcNow));
                await db.SaveChangesAsync();
            }));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            env.WriteAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, async db =>
            {
                var customer = await db.Customers.SingleAsync(c => c.TenantId == TenantSeed.KBeautyTenantId);
                db.Entry(customer).Property(nameof(Customer.TenantId)).CurrentValue = BellaTenantId;
                await db.SaveChangesAsync();
            }));

        var bellaFromKBeautyContext = await env.ReadAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, db =>
            db.Customers.FirstOrDefaultAsync(c => c.TenantId == BellaTenantId));
        Assert.Null(bellaFromKBeautyContext);
    }

    [Fact]
    [Trait("Category", "MultiTenant")]
    [Trait("Category", "NoDefaultTenant")]
    public async Task Without_tenant_context_commercial_queries_return_zero_and_writes_fail()
    {
        await using var env = await MultiTenantTestEnvironment.CreateAsync();

        var customerCount = await env.PlatformReadAsync(db => db.Customers.CountAsync());
        Assert.Equal(0, customerCount);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            using var scope = env.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Customers.Add(new Customer(
                Guid.NewGuid(),
                TenantSeed.KBeautyTenantId,
                "No Tenant Context",
                "no.context@kbeauty.local",
                new DateTime(1990, 1, 1),
                DateTime.UtcNow));
            await db.SaveChangesAsync();
        });
    }

    [Fact]
    [Trait("Category", "MultiTenant")]
    public async Task LoyaltyCard_serial_number_is_globally_unique()
    {
        await using var env = await MultiTenantTestEnvironment.CreateAsync();

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            env.WriteAsync(BellaTenantId, BellaSlug, async db =>
            {
                var customer = new Customer(
                    Guid.NewGuid(),
                    BellaTenantId,
                    "Duplicate Serial Customer",
                    "duplicate.serial@bella.local",
                    new DateTime(1990, 1, 1),
                    DateTime.UtcNow,
                    "6469990000");
                db.Customers.Add(customer);
                db.LoyaltyCards.Add(new LoyaltyCard(
                    Guid.NewGuid(),
                    BellaTenantId,
                    customer.Id,
                    KBeautySerial,
                    DateTime.UtcNow));
                await db.SaveChangesAsync();
            }));
    }

    [Fact]
    [Trait("Category", "MultiTenant")]
    [Trait("Category", "NoDefaultTenant")]
    public async Task Wallet_resolution_sets_the_correct_tenant_by_serial()
    {
        await using var env = await MultiTenantTestEnvironment.CreateAsync();

        var kbeauty = await env.WithScopeAsync(async sp =>
            await sp.GetRequiredService<ILoyaltyCardTenantLookup>().ResolveBySerialNumberAsync(KBeautySerial));
        var bella = await env.WithScopeAsync(async sp =>
            await sp.GetRequiredService<ILoyaltyCardTenantLookup>().ResolveBySerialNumberAsync(BellaSerial));

        Assert.Equal(TenantSeed.KBeautySlug, kbeauty?.TenantSlug);
        Assert.Equal(BellaSlug, bella?.TenantSlug);

        var resolvedCard = await env.WithScopeAsync(async sp =>
        {
            var resolver = sp.GetRequiredService<IWalletTenantContextResolver>();
            var resolvedTenant = await resolver.ResolveAndSetTenantAsync(BellaSerial, requireOperational: true);
            var card = await sp.GetRequiredService<ILoyaltyCardRepository>().GetBySerialNumberAsync(BellaSerial);
            return new { resolvedTenant, card };
        });

        Assert.Equal(BellaSlug, resolvedCard.resolvedTenant?.TenantSlug);
        Assert.Equal(BellaTenantId, resolvedCard.card?.TenantId);
        Assert.Equal(BellaSerial, resolvedCard.card?.SerialNumber);
    }

    [Fact]
    [Trait("Category", "MultiTenant")]
    [Trait("Category", "AdminCustomerPoints")]
    public async Task Scan_prefill_serial_lookup_does_not_cross_tenants()
    {
        await using var env = await MultiTenantTestEnvironment.CreateAsync();

        var result = await env.WithScopeAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, async sp =>
            await sp.GetRequiredService<ISender>().Send(new GetCustomerBySerialQuery(BellaSerial)));

        Assert.True(result.IsFailure);
    }

    [Fact]
    [Trait("Category", "MultiTenant")]
    [Trait("Category", "AdminCustomerPoints")]
    public async Task Add_points_flow_rejects_serial_from_another_tenant()
    {
        await using var env = await MultiTenantTestEnvironment.CreateAsync();

        var result = await env.WithScopeAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, async sp =>
            await sp.GetRequiredService<ISender>().Send(new AddPointsCommand(BellaSerial, 100m, "admin-panel")));

        Assert.True(result.IsFailure);

        var bellaPoints = await env.ReadAsync(BellaTenantId, BellaSlug, db =>
            db.LoyaltyCards.Where(c => c.SerialNumber == BellaSerial).Select(c => c.CurrentPoints).SingleAsync());
        Assert.Equal(0, bellaPoints);
    }

    [Fact]
    [Trait("Category", "MultiTenant")]
    [Trait("Category", "AdminCustomerPoints")]
    public async Task Add_points_flow_uses_purchase_amount_received_from_scan_form()
    {
        await using var env = await MultiTenantTestEnvironment.CreateAsync();

        var result = await env.WithScopeAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, async sp =>
            await sp.GetRequiredService<ISender>().Send(new AddPointsCommand(KBeautySerial, 500m, "admin-panel")));

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(50, result.Value.PointsAdded);

        var kbeautyPoints = await env.ReadAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, db =>
            db.LoyaltyCards.Where(c => c.SerialNumber == KBeautySerial).Select(c => c.CurrentPoints).SingleAsync());
        Assert.Equal(50, kbeautyPoints);
    }

    [Fact]
    [Trait("Category", "MultiTenant")]
    public async Task Device_registration_platform_lookup_returns_only_passkit_serials()
    {
        await using var env = await MultiTenantTestEnvironment.CreateAsync();

        var result = await env.WithScopeAsync(async sp =>
            await sp.GetRequiredService<IDeviceRegistrationPlatformReadService>()
                .GetUpdatableSerialsAsync(SharedDevice, PassType, passesUpdatedSince: null));

        Assert.Contains(KBeautySerial, result.SerialNumbers);
        Assert.Contains(BellaSerial, result.SerialNumbers);
        Assert.Equal(2, result.SerialNumbers.Count);
        Assert.True(result.LastUpdated > DateTime.MinValue);
    }

    [Fact]
    [Trait("Category", "MultiTenant")]
    [Trait("Category", "NoDefaultTenant")]
    public async Task Suspended_tenant_is_excluded_from_operational_jobs()
    {
        await using var env = await MultiTenantTestEnvironment.CreateAsync();

        await env.PlatformWriteAsync(db =>
            db.Database.ExecuteSqlRawAsync(
                "UPDATE TenantSubscriptions SET Status = 'Suspended' WHERE TenantId = {0}",
                BellaTenantId));

        var tenants = await env.WithScopeAsync(async sp =>
            await sp.GetRequiredService<IOperationalTenantReadService>().ListTenantsForExecutionAsync());

        var bella = Assert.Single(tenants, t => t.Slug == BellaSlug);
        Assert.False(bella.IsOperational);

        var executed = new List<string>();
        var summary = await env.WithScopeAsync(async sp =>
            await sp.GetRequiredService<ITenantExecutionRunner>().RunForOperationalTenantsAsync(
                "mt2h-test",
                (tenantSp, tenant, _) =>
                {
                    executed.Add(tenant.Slug);
                    return Task.CompletedTask;
                }));

        Assert.Contains(TenantSeed.KBeautySlug, executed);
        Assert.DoesNotContain(BellaSlug, executed);
        Assert.Equal(1, summary.EligibleTenantCount);
        Assert.Equal(1, summary.SucceededTenantCount);
        Assert.Equal(1, summary.SkippedTenantCount);
    }

    [Fact]
    [Trait("Category", "MultiTenant")]
    public async Task TenantContext_is_immutable_within_scope()
    {
        await using var env = await MultiTenantTestEnvironment.CreateAsync();

        await env.WithScopeAsync(sp =>
        {
            var context = sp.GetRequiredService<IMutableTenantContext>();
            context.SetTenant(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug);
            context.SetTenant(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug);

            Assert.Throws<InvalidOperationException>(() =>
                context.SetTenant(BellaTenantId, BellaSlug));

            return Task.CompletedTask;
        });
    }

    private sealed class MultiTenantTestEnvironment : IAsyncDisposable
    {
        private readonly ServiceProvider _services;
        private readonly string _connectionString;

        private MultiTenantTestEnvironment(ServiceProvider services, string connectionString)
        {
            _services = services;
            _connectionString = connectionString;
        }

        public IServiceProvider Services => _services;

        public static async Task<MultiTenantTestEnvironment> CreateAsync()
        {
            var dbName = "LoyaltyCloud_MT2H_" + Guid.NewGuid().ToString("N");
            var connectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True;";
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                    ["Azure:KeyVaultUri"] = "",
                    ["Azure:BlobStorage:ConnectionString"] = "UseDevelopmentStorage=true",
                    ["Apple:PassTypeIdentifier"] = PassType,
                    ["Apple:TeamIdentifier"] = "TESTTEAM01",
                    ["Apple:WebServiceURL"] = "https://test.local",
                    ["Apple:OrganizationName"] = "LoyaltyCloud Test",
                    ["Wallet:UseRealPassSigning"] = "false",
                    ["Wallet:UseRealApns"] = "false"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddApplication();
            services.AddInfrastructure(configuration, new TestHostEnvironment());

            var provider = services.BuildServiceProvider(validateScopes: true);
            var env = new MultiTenantTestEnvironment(provider, connectionString);
            await env.InitializeAsync();
            return env;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.EnsureDeletedAsync();
            }
            finally
            {
                await _services.DisposeAsync();
            }
        }

        public async Task<T> ReadAsync<T>(
            Guid tenantId,
            string tenantSlug,
            Func<AppDbContext, Task<T>> query)
        {
            using var scope = _services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(tenantId, tenantSlug);
            return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }

        public async Task WriteAsync(
            Guid tenantId,
            string tenantSlug,
            Func<AppDbContext, Task> operation)
        {
            using var scope = _services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(tenantId, tenantSlug);
            await operation(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }

        public async Task<T> PlatformReadAsync<T>(Func<AppDbContext, Task<T>> query)
        {
            using var scope = _services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }

        public async Task PlatformWriteAsync(Func<AppDbContext, Task> operation)
        {
            using var scope = _services.CreateScope();
            await operation(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }

        public async Task<T> WithScopeAsync<T>(
            Guid tenantId,
            string tenantSlug,
            Func<IServiceProvider, Task<T>> operation)
        {
            using var scope = _services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(tenantId, tenantSlug);
            return await operation(scope.ServiceProvider);
        }

        public async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> operation)
        {
            using var scope = _services.CreateScope();
            return await operation(scope.ServiceProvider);
        }

        public async Task WithScopeAsync(Func<IServiceProvider, Task> operation)
        {
            using var scope = _services.CreateScope();
            await operation(scope.ServiceProvider);
        }

        public async Task SeedBellaLoyaltyLevelsAsync()
        {
            await WriteAsync(BellaTenantId, BellaSlug, async db =>
            {
                if (await db.TenantLoyaltyLevels.AnyAsync(level => level.TenantId == BellaTenantId))
                    return;

                var now = DateTime.UtcNow;
                db.TenantLoyaltyLevels.AddRange(
                    new TenantLoyaltyLevel(Guid.Parse("b2000002-0000-0000-0000-000000000801"), BellaTenantId, "Bronce", 0, 1, now),
                    new TenantLoyaltyLevel(Guid.Parse("b2000002-0000-0000-0000-000000000802"), BellaTenantId, "Plata", 500, 2, now),
                    new TenantLoyaltyLevel(Guid.Parse("b2000002-0000-0000-0000-000000000803"), BellaTenantId, "Oro", 1500, 3, now));
                await db.SaveChangesAsync();
            });
        }

        public async Task SeedBellaFourthAndFifthLevelsAsync()
        {
            await WriteAsync(BellaTenantId, BellaSlug, async db =>
            {
                if (await db.TenantLoyaltyLevels.AnyAsync(level => level.TenantId == BellaTenantId && level.Name == "Platino"))
                    return;

                var now = DateTime.UtcNow;
                db.TenantLoyaltyLevels.AddRange(
                    new TenantLoyaltyLevel(Guid.Parse("b2000002-0000-0000-0000-000000000804"), BellaTenantId, "Platino", 3000, 4, now),
                    new TenantLoyaltyLevel(Guid.Parse("b2000002-0000-0000-0000-000000000805"), BellaTenantId, "Diamante", 5000, 5, now));
                await db.SaveChangesAsync();
            });
        }

        public async Task AddBellaRollingPointsAsync(int points)
        {
            await WriteAsync(BellaTenantId, BellaSlug, async db =>
            {
                var card = await db.LoyaltyCards.SingleAsync(c => c.SerialNumber == BellaSerial);
                db.PointTransactions.Add(new PointTransaction(
                    Guid.NewGuid(),
                    BellaTenantId,
                    card.Id,
                    points,
                    TransactionType.Purchase,
                    "Additional rolling points.",
                    DateTime.UtcNow,
                    purchaseAmount: points * 20m,
                    createdBy: "test"));
                await db.SaveChangesAsync();
            });
        }

        public async Task SeedBellaRewardsForDynamicLevelsAsync(bool includeFourthAndFifth = false)
        {
            await SeedSingleBellaRewardAsync("Todos reward", string.Empty, 10);
            await SeedSingleBellaRewardAsync("Plata reward", "Plata", 10);
            await SeedSingleBellaRewardAsync("Oro reward", "Oro", 10);

            if (includeFourthAndFifth)
            {
                await SeedSingleBellaRewardAsync("Platino reward", "Platino", 10);
                await SeedSingleBellaRewardAsync("Diamante reward", "Diamante", 10);
            }
        }

        public async Task<Guid> SeedSingleBellaRewardAsync(
            string name,
            string minLevel,
            int pointsCost,
            bool isMonthlyProduct = false,
            DateTime? validFrom = null,
            DateTime? validTo = null)
        {
            var rewardId = Guid.NewGuid();
            await WriteAsync(BellaTenantId, BellaSlug, async db =>
            {
                db.RewardCatalogItems.Add(new RewardCatalogItem(
                    rewardId,
                    BellaTenantId,
                    name,
                    "Dynamic level reward.",
                    pointsCost,
                    minLevel,
                    isMonthlyProduct,
                    validFrom,
                    validTo));
                await db.SaveChangesAsync();
            });
            return rewardId;
        }

        private async Task InitializeAsync()
        {
            using (var scope = _services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.EnsureDeletedAsync();
                await db.Database.MigrateAsync();
                scope.ServiceProvider.GetRequiredService<IMutableTenantContext>()
                    .SetTenant(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug);
                await IntegrationTestSeed.EnsureKBeautyPlatformRowsAsync(db);
                await IntegrationTestSeed.EnsureDefaultTenantLevelsAsync(db);
            }

            await SeedBellaPlatformRowsAsync();
            await SeedTenantOwnedDataAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, isBella: false);
            await SeedTenantOwnedDataAsync(BellaTenantId, BellaSlug, isBella: true);
            await SeedSharedDeviceRegistrationsAsync();
        }

        private async Task SeedBellaPlatformRowsAsync()
        {
            await PlatformWriteAsync(async db =>
            {
                db.Tenants.Add(new Tenant(
                    BellaTenantId,
                    BellaSlug,
                    "Bella Salon",
                    "America/Tijuana",
                    DateTime.UtcNow));
                db.TenantBrandings.Add(new TenantBranding(
                    BellaTenantId,
                    primaryColor: "#8B5CF6",
                    secondaryColor: "#F5D0FE"));
                db.TenantSubscriptions.Add(new TenantSubscription(
                    BellaTenantId,
                    TenantSubscriptionStatus.Active,
                    "development",
                    paidThroughUtc: DateTime.UtcNow.AddDays(30)));
                await db.SaveChangesAsync();
            });
        }

        private async Task SeedTenantOwnedDataAsync(Guid tenantId, string tenantSlug, bool isBella)
        {
            await WriteAsync(tenantId, tenantSlug, async db =>
            {
                var now = DateTime.UtcNow;
                var customer = new Customer(
                    isBella ? Guid.Parse("b2000002-0000-0000-0000-000000000101") : Guid.Parse("b1000002-0000-0000-0000-000000000101"),
                    tenantId,
                    isBella ? "Bella Isolation Customer" : "KBeauty Isolation Customer",
                    isBella ? "isolation@bella.local" : "isolation@kbeauty.local",
                    new DateTime(1990, 1, 1),
                    now,
                    SharedPhone);
                var card = new LoyaltyCard(
                    isBella ? Guid.Parse("b2000002-0000-0000-0000-000000000201") : Guid.Parse("b1000002-0000-0000-0000-000000000201"),
                    tenantId,
                    customer.Id,
                    isBella ? BellaSerial : KBeautySerial,
                    now);
                var reward = new RewardCatalogItem(
                    isBella ? Guid.Parse("b2000002-0000-0000-0000-000000000301") : Guid.Parse("b1000002-0000-0000-0000-000000000301"),
                    tenantId,
                    isBella ? "Bella Reward" : "KBeauty Reward",
                    "Reward for tenant isolation tests.",
                    isBella ? 150 : 300,
                    LoyaltyConstants.Levels.Mist);

                db.Customers.Add(customer);
                db.LoyaltyCards.Add(card);
                db.RewardCatalogItems.Add(reward);

                if (isBella)
                {
                    db.ProgramConfigs.Add(new ProgramConfig(
                        Guid.Parse("b2000002-0000-0000-0000-000000000401"),
                        tenantId,
                        LoyaltyConstants.ConfigKeys.PointsPerPesoUnit,
                        "20",
                        now,
                        "Bella points per peso.",
                        "test"));
                }

                var transactionId = isBella
                    ? Guid.Parse("b2000002-0000-0000-0000-000000000501")
                    : Guid.Parse("b1000002-0000-0000-0000-000000000501");
                db.PointTransactions.Add(new PointTransaction(
                    transactionId,
                    tenantId,
                    card.Id,
                    isBella ? 450 : 300,
                    TransactionType.Purchase,
                    "Seed purchase.",
                    now,
                    purchaseAmount: isBella ? 9000m : 3000m,
                    createdBy: "test"));
                db.PointLots.Add(new PointLot(
                    isBella ? Guid.Parse("b2000002-0000-0000-0000-000000000601") : Guid.Parse("b1000002-0000-0000-0000-000000000601"),
                    tenantId,
                    card.Id,
                    transactionId,
                    isBella ? 450 : 300,
                    now,
                    now.AddMonths(12),
                    now));

                await db.SaveChangesAsync();
            });
        }

        private async Task SeedSharedDeviceRegistrationsAsync()
        {
            await WriteAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, async db =>
            {
                db.DeviceRegistrations.Add(new DeviceRegistration(
                    Guid.Parse("b1000002-0000-0000-0000-000000000701"),
                    TenantSeed.KBeautyTenantId,
                    SharedDevice,
                    PassType,
                    KBeautySerial,
                    "push-token-kbeauty",
                    DateTime.UtcNow));
                await db.SaveChangesAsync();
            });

            await WriteAsync(BellaTenantId, BellaSlug, async db =>
            {
                db.DeviceRegistrations.Add(new DeviceRegistration(
                    Guid.Parse("b2000002-0000-0000-0000-000000000701"),
                    BellaTenantId,
                    SharedDevice,
                    PassType,
                    BellaSerial,
                    "push-token-bella",
                    DateTime.UtcNow));
                await db.SaveChangesAsync();
            });
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "LoyaltyCloud.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
