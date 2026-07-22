using System.Data;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Constants;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.ValueObjects;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Infrastructure.Persistence;

/// <summary>
/// Carga datos visuales solo para Development. No participa en migraciones ni
/// en providers InMemory usados por tests.
/// </summary>
public static class DevelopmentDataSeeder
{
    private const string DemoEmailPrefix = "demo.customer.";
    private const string DemoEmailDomain = "@kbeauty.local";
    private const string DemoOperator = "development-seed";
    private const string SentinelEmail = "demo.customer.001@kbeauty.local";
    private const string SharedIsolationPhone = "6461234567";
    private const string KBeautyIsolationSerial = "KB-TEST-001";
    private const string BellaIsolationSerial = "BS-TEST-001";
    private static readonly Guid BellaTenantId = Guid.Parse("b2000000-0000-0000-0000-000000000001");
    private const string BellaSlug = "bella-salon";

    private static readonly string[] DemoRewardNames =
    {
        "Facial discount",
        "Free product sample",
        "Birthday glow gift",
        "Premium treatment discount",
        "VIP skincare session"
    };

    public static async Task SeedDevelopmentDataAsync(
        this IServiceProvider services,
        IHostEnvironment environment,
        CancellationToken ct = default)
    {
        if (!environment.IsDevelopment())
            return;

        using var scope = services.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<IMutableTenantContext>();
        tenantContext.SetTenant(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHashingService>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("LoyaltyCloud.DevelopmentDataSeeder");

        if (string.Equals(db.Database.ProviderName, "Microsoft.EntityFrameworkCore.InMemory", StringComparison.Ordinal))
        {
            logger.LogDebug("Skipping development demo seed for InMemory provider.");
            return;
        }

        var customersCreated = 0;
        var transactionsCreated = 0;
        var redemptionsCreated = 0;
        var configCreated = 0;
        var rewardsCreated = 0;

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            configCreated = await EnsureProgramConfigAsync(db, ct);
            rewardsCreated = await EnsureRewardsAsync(db, ct);
            await EnsureDevelopmentAdminUserAsync(
                db,
                passwordHasher,
                logger,
                TenantSeed.KBeautyTenantId,
                "KBeauty",
                configuration["DevelopmentTenants:KBeauty:AdminUsername"],
                configuration["DevelopmentTenants:KBeauty:AdminPassword"],
                ct);
            await db.SaveChangesAsync(ct);

            var sentinelExists = await db.Customers.AnyAsync(c => c.TenantId == TenantSeed.KBeautyTenantId && c.Email == SentinelEmail, ct);
            if (!sentinelExists)
            {
                var counts = await SeedCustomersAsync(db, ct);
                customersCreated = counts.Customers;
                transactionsCreated = counts.Transactions;
            }

            await db.SaveChangesAsync(ct);

            var redemptionCounts = await EnsureRedemptionsAsync(db, ct);
            transactionsCreated += redemptionCounts.Transactions;
            redemptionsCreated += redemptionCounts.Redemptions;

            var isolationCounts = await EnsureKBeautyIsolationSampleAsync(db, ct);
            customersCreated += isolationCounts.Customers;
            transactionsCreated += isolationCounts.Transactions;

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        logger.LogInformation(
            "Development demo seed completed. Config: {Config}, Rewards: {Rewards}, Customers: {Customers}, Transactions: {Transactions}, Redemptions: {Redemptions}.",
            configCreated,
            rewardsCreated,
            customersCreated,
            transactionsCreated,
            redemptionsCreated);

        await SeedBellaSalonDevelopmentTenantAsync(services, ct);
    }

    private static async Task<int> EnsureProgramConfigAsync(AppDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var existing = await db.ProgramConfigs
            .Where(c => c.TenantId == TenantSeed.KBeautyTenantId)
            .Select(c => c.Key)
            .ToListAsync(ct);
        var existingSet = existing.ToHashSet(StringComparer.Ordinal);

        var entries = new (string Key, string Value, string Description)[]
        {
            (LoyaltyConstants.ConfigKeys.PointsPerPesoUnit, "10", "Pesos MXN por 1 punto."),
            (LoyaltyConstants.ConfigKeys.WelcomeBonusPoints, "50", "Puntos al registrarse."),
            (LoyaltyConstants.ConfigKeys.ReferralBonusPoints, "150", "Puntos por referido confirmado."),
            (LoyaltyConstants.ConfigKeys.BirthdayMultiplier, "2", "Multiplicador en mes de cumpleanos."),
            (LoyaltyConstants.ConfigKeys.LevelMistMin, "0", "Umbral inicio nivel Mist."),
            (LoyaltyConstants.ConfigKeys.LevelGlowMin, "1000", "Umbral inicio nivel Glow."),
            (LoyaltyConstants.ConfigKeys.LevelRadianceMin, "3000", "Umbral inicio nivel Radiance."),
            (LoyaltyConstants.ConfigKeys.RadianceRequalificationPoints, "500", "Puntos anuales para mantener Radiance."),
            (LoyaltyConstants.ConfigKeys.RewardMiniProductPoints, "120", "Costo demo de muestra mini."),
            (LoyaltyConstants.ConfigKeys.RewardFiftyOffPoints, "250", "Costo demo de descuento facial."),
            (LoyaltyConstants.ConfigKeys.RewardFocusSkinPoints, "450", "Costo demo de regalo birthday glow."),
            (LoyaltyConstants.ConfigKeys.RewardMonthlyProductPoints, "750", "Costo demo de tratamiento premium."),
            (LoyaltyConstants.ConfigKeys.RewardHundredOffCabinaPoints, "950", "Costo demo de descuento cabina."),
            (LoyaltyConstants.ConfigKeys.RewardFacialOffPoints, "1200", "Costo demo de sesion VIP.")
        };

        var created = 0;
        foreach (var entry in entries)
        {
            if (existingSet.Contains(entry.Key))
                continue;

            db.ProgramConfigs.Add(new ProgramConfig(
                Guid.NewGuid(),
                TenantSeed.KBeautyTenantId,
                entry.Key,
                entry.Value,
                now,
                entry.Description,
                DemoOperator));
            created++;
        }

        return created;
    }

    private static async Task<int> EnsureRewardsAsync(AppDbContext db, CancellationToken ct)
    {
        var existing = await db.RewardCatalogItems
            .Where(r => r.TenantId == TenantSeed.KBeautyTenantId && DemoRewardNames.Contains(r.Name))
            .Select(r => r.Name)
            .ToListAsync(ct);
        var existingSet = existing.ToHashSet(StringComparer.Ordinal);

        var now = DateTime.UtcNow;
        var rewards = new (string Name, string Description, int Cost, string MinLevel, bool Monthly)[]
        {
            ("Facial discount", "$250 MXN off en facial de cabina.", 250, LoyaltyConstants.Levels.Mist, false),
            ("Free product sample", "Mini producto de skincare para probar rutina nueva.", 120, LoyaltyConstants.Levels.Mist, false),
            ("Birthday glow gift", "Gift especial durante el mes de cumpleanos.", 450, LoyaltyConstants.Levels.Glow, false),
            ("Premium treatment discount", "Descuento premium en tratamiento avanzado.", 750, LoyaltyConstants.Levels.Glow, true),
            ("VIP skincare session", "Sesion VIP de diagnostico y rutina personalizada.", 1200, LoyaltyConstants.Levels.Radiance, false)
        };

        var created = 0;
        foreach (var reward in rewards)
        {
            if (existingSet.Contains(reward.Name))
                continue;

            db.RewardCatalogItems.Add(new RewardCatalogItem(
                Guid.NewGuid(),
                TenantSeed.KBeautyTenantId,
                reward.Name,
                reward.Description,
                reward.Cost,
                reward.MinLevel,
                reward.Monthly,
                now.AddDays(-15),
                now.AddDays(60)));
            created++;
        }

        return created;
    }

    private static async Task<(int Customers, int Transactions)> SeedCustomersAsync(
        AppDbContext db,
        CancellationToken ct)
    {
        var config = ProgramConfigSnapshot.FromEntries(await db.ProgramConfigs.Where(c => c.TenantId == TenantSeed.KBeautyTenantId).ToListAsync(ct));
        var now = DateTime.UtcNow;
        var clock = new MutableClock(now);
        var customerSpecs = DemoCustomers();
        var transactionsCreated = 0;

        for (var i = 0; i < customerSpecs.Length; i++)
        {
            var spec = customerSpecs[i];
            var ordinal = i + 1;
            var customerId = Guid.NewGuid();
            var cardId = Guid.NewGuid();
            var createdAt = now.Date.AddDays(-60 + ordinal * 2).AddHours(16);
            var customer = new Customer(
                customerId,
                TenantSeed.KBeautyTenantId,
                spec.Name,
                $"{DemoEmailPrefix}{ordinal:000}{DemoEmailDomain}",
                spec.BirthDate,
                createdAt,
                $"+52 646 555 {1000 + ordinal:0000}");
            var card = new LoyaltyCard(cardId, TenantSeed.KBeautyTenantId, customerId, $"KB-DEMO{ordinal:000}", createdAt);

            db.Customers.Add(customer);
            db.LoyaltyCards.Add(card);

            var target = spec.TargetPositivePoints;
            transactionsCreated += AddEarned(
                db,
                card,
                50,
                TransactionType.BonusWelcome,
                "Bono de bienvenida demo",
                createdAt.AddMinutes(5),
                config,
                clock,
                BonusType.Welcome);

            var purchaseOne = Math.Max(80, target / 4);
            var purchaseTwo = Math.Max(90, target / 3);
            var purchaseThree = Math.Max(100, target - 50 - purchaseOne - purchaseTwo - spec.BonusPoints);

            transactionsCreated += AddEarned(
                db,
                card,
                purchaseOne,
                TransactionType.Purchase,
                "Compra demo: rutina hidratante",
                now.AddDays(-28 + ordinal),
                config,
                clock,
                purchaseAmount: purchaseOne * 10m);
            transactionsCreated += AddEarned(
                db,
                card,
                purchaseTwo,
                TransactionType.Purchase,
                "Compra demo: tratamiento cabina",
                now.AddDays(-14 + ordinal / 2),
                config,
                clock,
                purchaseAmount: purchaseTwo * 10m);
            transactionsCreated += AddEarned(
                db,
                card,
                purchaseThree,
                TransactionType.Purchase,
                "Compra demo: replenishment skincare",
                now.AddDays(-ordinal % 10),
                config,
                clock,
                purchaseAmount: purchaseThree * 10m);

            var bonusType = ordinal % 3 == 0 ? BonusType.Birthday : BonusType.Manual;
            var transactionType = bonusType == BonusType.Birthday
                ? TransactionType.BonusBirthday
                : TransactionType.BonusReferral;
            transactionsCreated += AddEarned(
                db,
                card,
                spec.BonusPoints,
                transactionType,
                bonusType == BonusType.Birthday ? "Bono cumpleanos demo" : "Ajuste manual demo",
                now.AddDays(-ordinal),
                config,
                clock,
                bonusType);
        }

        return (customerSpecs.Length, transactionsCreated);
    }

    private static async Task<(int Transactions, int Redemptions)> EnsureRedemptionsAsync(
        AppDbContext db,
        CancellationToken ct)
    {
        var demoCards = await db.LoyaltyCards
            .Where(c => c.TenantId == TenantSeed.KBeautyTenantId && c.SerialNumber.StartsWith("KB-DEMO"))
            .OrderBy(c => c.SerialNumber)
            .Take(9)
            .ToListAsync(ct);

        if (demoCards.Count == 0)
            return (0, 0);

        var demoCardIds = demoCards.Select(c => c.Id).ToList();
        var hasDemoRedemptions = await db.Redemptions
            .AnyAsync(r => demoCardIds.Contains(r.LoyaltyCardId), ct);
        if (hasDemoRedemptions)
            return (0, 0);

        var rewards = await db.RewardCatalogItems
            .Where(r => r.TenantId == TenantSeed.KBeautyTenantId && DemoRewardNames.Contains(r.Name))
            .OrderBy(r => r.PointsCost)
            .ToListAsync(ct);
        if (rewards.Count == 0)
            return (0, 0);

        var config = ProgramConfigSnapshot.FromEntries(await db.ProgramConfigs.Where(c => c.TenantId == TenantSeed.KBeautyTenantId).ToListAsync(ct));
        var clock = new MutableClock(DateTime.UtcNow);
        var now = DateTime.UtcNow;
        var transactionsCreated = 0;
        var redemptionsCreated = 0;

        for (var i = 0; i < demoCards.Count; i++)
        {
            var ordinal = i + 1;
            var card = demoCards[i];
            var reward = rewards
                .Where(r => r.PointsCost <= card.CurrentPoints)
                .ElementAtOrDefault(Math.Min(i % rewards.Count, rewards.Count - 1))
                ?? rewards.LastOrDefault(r => r.PointsCost <= card.CurrentPoints);

            if (reward is null)
                continue;

            var redeemedAt = now.AddDays(-ordinal / 2.0);
            card.RedeemPoints(reward.PointsCost);
            db.PointTransactions.Add(new PointTransaction(
                Guid.NewGuid(),
                card.TenantId,
                card.Id,
                -reward.PointsCost,
                TransactionType.Redemption,
                $"Canje demo: {reward.Name}",
                redeemedAt,
                purchaseAmount: null,
                createdBy: DemoOperator));
            transactionsCreated++;

            var redemption = new Redemption(
                Guid.NewGuid(),
                card.TenantId,
                card.Id,
                reward.Id,
                reward.PointsCost,
                redeemedAt);

            if (ordinal is 4 or 5 or 6)
            {
                redemption.Confirm(DemoOperator, redeemedAt.AddHours(2), "Entregado en demo.");
            }
            else if (ordinal is 7 or 8 or 9)
            {
                redemption.Cancel(DemoOperator, redeemedAt.AddHours(3), "Cancelado en demo.");
                transactionsCreated += AddEarned(
                    db,
                    card,
                    reward.PointsCost,
                    TransactionType.BonusReferral,
                    $"Reembolso demo: {reward.Name}",
                    redeemedAt.AddHours(3),
                    config,
                    clock,
                    BonusType.Manual);
            }

            db.Redemptions.Add(redemption);
            redemptionsCreated++;
        }

        return (transactionsCreated, redemptionsCreated);
    }

    private static async Task<(int Customers, int Transactions)> EnsureKBeautyIsolationSampleAsync(
        AppDbContext db,
        CancellationToken ct)
    {
        if (await db.LoyaltyCards.AnyAsync(c => c.TenantId == TenantSeed.KBeautyTenantId && c.SerialNumber == KBeautyIsolationSerial, ct))
            return (0, 0);

        var now = DateTime.UtcNow;
        var config = ProgramConfigSnapshot.FromEntries(await db.ProgramConfigs.Where(c => c.TenantId == TenantSeed.KBeautyTenantId).ToListAsync(ct));
        var clock = new MutableClock(now);
        var customer = new Customer(
            Guid.Parse("b1000001-0000-0000-0000-000000000101"),
            TenantSeed.KBeautyTenantId,
            "KBeauty Test Customer",
            "isolation.kbeauty@kbeauty.local",
            new DateTime(1992, 7, 21),
            now,
            SharedIsolationPhone);
        var card = new LoyaltyCard(
            Guid.Parse("b1000001-0000-0000-0000-000000000201"),
            TenantSeed.KBeautyTenantId,
            customer.Id,
            KBeautyIsolationSerial,
            now);

        db.Customers.Add(customer);
        db.LoyaltyCards.Add(card);

        var transactions = AddEarned(
            db,
            card,
            300,
            TransactionType.Purchase,
            "Compra seed aislamiento KBeauty",
            now,
            config,
            clock,
            purchaseAmount: 3000m);

        return (1, transactions);
    }

    private static async Task SeedBellaSalonDevelopmentTenantAsync(
        IServiceProvider services,
        CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<IMutableTenantContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHashingService>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("LoyaltyCloud.DevelopmentDataSeeder");

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            await EnsureBellaPlatformRowsAsync(db, ct);
            tenantContext.SetTenant(BellaTenantId, BellaSlug);

            var configCreated = await EnsureBellaProgramConfigAsync(db, ct);
            var rewardsCreated = await EnsureBellaRewardsAsync(db, ct);
            var customersCreated = await EnsureBellaCustomerAsync(db, ct);
            var campaignsCreated = await EnsureBellaCampaignAsync(db, ct);
            await EnsureDevelopmentAdminUserAsync(
                db,
                passwordHasher,
                logger,
                BellaTenantId,
                "BellaSalon",
                configuration["DevelopmentTenants:BellaSalon:AdminUsername"],
                configuration["DevelopmentTenants:BellaSalon:AdminPassword"],
                ct);

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            logger.LogInformation(
                "Bella Salon development seed completed. Config: {Config}, Rewards: {Rewards}, Customers: {Customers}, Campaigns: {Campaigns}.",
                configCreated,
                rewardsCreated,
                customersCreated,
                campaignsCreated);
        });
    }

    private static async Task EnsureBellaPlatformRowsAsync(AppDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (!await db.Tenants.AnyAsync(t => t.Id == BellaTenantId, ct))
        {
            db.Tenants.Add(new Tenant(
                BellaTenantId,
                BellaSlug,
                "Bella Salon",
                "America/Tijuana",
                now));
        }

        if (!await db.TenantBrandings.AnyAsync(b => b.TenantId == BellaTenantId, ct))
        {
            db.TenantBrandings.Add(new TenantBranding(
                BellaTenantId,
                primaryColor: "#8B5CF6",
                secondaryColor: "#F5D0FE"));
        }

        if (!await db.TenantSubscriptions.AnyAsync(s => s.TenantId == BellaTenantId, ct))
        {
            db.TenantSubscriptions.Add(new TenantSubscription(
                BellaTenantId,
                TenantSubscriptionStatus.Active,
                "development"));
        }
    }

    private static async Task EnsureDevelopmentAdminUserAsync(
        AppDbContext db,
        IPasswordHashingService passwordHasher,
        ILogger logger,
        Guid tenantId,
        string tenantKey,
        string? username,
        string? password,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "Development admin user seed skipped for TenantId={TenantId}, TenantKey={TenantKey}. Configure DevelopmentTenants section admin username and password for this tenant.",
                tenantId,
                tenantKey);
            return;
        }

        var normalizedUsername = TenantAdminUser.NormalizeUsername(username);
        if (await db.TenantAdminUsers.AnyAsync(u => u.TenantId == tenantId && u.NormalizedUsername == normalizedUsername, ct))
            return;

        db.TenantAdminUsers.Add(new TenantAdminUser(
            Guid.NewGuid(),
            tenantId,
            username.Trim(),
            passwordHasher.HashPassword(password),
            DateTime.UtcNow));
    }

    private static async Task<int> EnsureBellaProgramConfigAsync(AppDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var existing = await db.ProgramConfigs
            .Where(c => c.TenantId == BellaTenantId)
            .Select(c => c.Key)
            .ToListAsync(ct);
        var existingSet = existing.ToHashSet(StringComparer.Ordinal);

        var entries = new (string Key, string Value, string Description)[]
        {
            (LoyaltyConstants.ConfigKeys.PointsPerPesoUnit, "20", "Pesos MXN por 1 punto Bella."),
            (LoyaltyConstants.ConfigKeys.WelcomeBonusPoints, "25", "Puntos al registrarse Bella."),
            (LoyaltyConstants.ConfigKeys.ReferralBonusPoints, "75", "Puntos por referido Bella."),
            (LoyaltyConstants.ConfigKeys.BirthdayMultiplier, "3", "Multiplicador en mes de cumpleanos Bella."),
            (LoyaltyConstants.ConfigKeys.LevelMistMin, "0", "Umbral Mist Bella."),
            (LoyaltyConstants.ConfigKeys.LevelGlowMin, "500", "Umbral Glow Bella."),
            (LoyaltyConstants.ConfigKeys.LevelRadianceMin, "1500", "Umbral Radiance Bella."),
            (LoyaltyConstants.ConfigKeys.RadianceRequalificationPoints, "300", "Recalificacion Radiance Bella.")
        };

        var created = 0;
        foreach (var entry in entries)
        {
            if (existingSet.Contains(entry.Key))
                continue;

            db.ProgramConfigs.Add(new ProgramConfig(
                Guid.NewGuid(),
                BellaTenantId,
                entry.Key,
                entry.Value,
                now,
                entry.Description,
                DemoOperator));
            created++;
        }

        return created;
    }

    private static async Task<int> EnsureBellaRewardsAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.RewardCatalogItems.AnyAsync(r => r.TenantId == BellaTenantId && r.Name == "Bella blowout reward", ct))
            return 0;

        var now = DateTime.UtcNow;
        db.RewardCatalogItems.Add(new RewardCatalogItem(
            Guid.Parse("b2000001-0000-0000-0000-000000000301"),
            BellaTenantId,
            "Bella blowout reward",
            "Reward exclusivo de Bella Salon para validar aislamiento.",
            150,
            LoyaltyConstants.Levels.Mist,
            false,
            now.AddDays(-1),
            now.AddDays(30)));

        return 1;
    }

    private static async Task<int> EnsureBellaCustomerAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.LoyaltyCards.AnyAsync(c => c.TenantId == BellaTenantId && c.SerialNumber == BellaIsolationSerial, ct))
            return 0;

        var now = DateTime.UtcNow;
        var customer = new Customer(
            Guid.Parse("b2000001-0000-0000-0000-000000000101"),
            BellaTenantId,
            "Bella Test Customer",
            "isolation.bella@bella-salon.local",
            new DateTime(1991, 5, 11),
            now,
            SharedIsolationPhone);
        var card = new LoyaltyCard(
            Guid.Parse("b2000001-0000-0000-0000-000000000201"),
            BellaTenantId,
            customer.Id,
            BellaIsolationSerial,
            now);
        var config = ProgramConfigSnapshot.FromEntries(await db.ProgramConfigs.Where(c => c.TenantId == BellaTenantId).ToListAsync(ct));
        var clock = new MutableClock(now);

        db.Customers.Add(customer);
        db.LoyaltyCards.Add(card);
        var transactionId = Guid.Parse("b2000001-0000-0000-0000-000000000401");
        clock.UtcNow = now;
        card.EarnPoints(450, TransactionType.Purchase, config, clock);
        db.PointTransactions.Add(new PointTransaction(
            transactionId,
            BellaTenantId,
            card.Id,
            450,
            TransactionType.Purchase,
            "Compra seed aislamiento Bella Salon",
            now,
            purchaseAmount: 9000m,
            createdBy: DemoOperator));
        db.PointLots.Add(new PointLot(
            Guid.Parse("b2000001-0000-0000-0000-000000000501"),
            BellaTenantId,
            card.Id,
            transactionId,
            450,
            now,
            now.AddMonths(12),
            now));

        return 1;
    }

    private static async Task<int> EnsureBellaCampaignAsync(AppDbContext db, CancellationToken ct)
    {
        if (await db.PointCampaigns.AnyAsync(c => c.TenantId == BellaTenantId && c.Name == "Bella double points", ct))
            return 0;

        var now = DateTime.UtcNow;
        db.PointCampaigns.Add(new PointCampaign(
            Guid.Parse("b2000001-0000-0000-0000-000000000601"),
            BellaTenantId,
            "Bella double points",
            "Campana demo Bella Salon.",
            2,
            null,
            CampaignLevelEligibility.All,
            now.AddDays(-1),
            now.AddDays(14),
            now));

        return 1;
    }

    private static int AddEarned(
        AppDbContext db,
        LoyaltyCard card,
        int points,
        TransactionType type,
        string description,
        DateTime createdAt,
        ProgramConfigSnapshot config,
        MutableClock clock,
        BonusType? bonusType = null,
        decimal? purchaseAmount = null)
    {
        clock.UtcNow = createdAt;
        card.EarnPoints(points, type, config, clock);
        db.PointTransactions.Add(new PointTransaction(
            Guid.NewGuid(),
            card.TenantId,
            card.Id,
            points,
            type,
            description,
            createdAt,
            bonusType,
            purchaseAmount,
            DemoOperator));
        return 1;
    }

    private static DemoCustomerSpec[] DemoCustomers() =>
    [
        new("Camila Torres", new DateTime(1994, 2, 12), 220, 60),
        new("Regina Salazar", new DateTime(1988, 7, 4), 380, 80),
        new("Valeria Montes", new DateTime(1991, 11, 21), 650, 100),
        new("Sofia Herrera", new DateTime(1997, 5, 18), 920, 120),
        new("Mariana Vega", new DateTime(1985, 9, 2), 1100, 140),
        new("Lucia Navarro", new DateTime(1993, 1, 29), 1350, 160),
        new("Isabella Cruz", new DateTime(1990, 12, 10), 1680, 180),
        new("Daniela Rios", new DateTime(1989, 6, 8), 1960, 200),
        new("Andrea Paredes", new DateTime(1996, 3, 14), 2250, 220),
        new("Paola Medina", new DateTime(1987, 10, 30), 2700, 240),
        new("Ximena Castillo", new DateTime(1992, 4, 6), 3100, 260),
        new("Natalia Duarte", new DateTime(1995, 8, 23), 3450, 280),
        new("Fernanda Leon", new DateTime(1986, 2, 19), 3850, 300),
        new("Renata Molina", new DateTime(1998, 12, 3), 4300, 320),
        new("Ana Sofia Ortega", new DateTime(1991, 6, 27), 4900, 340),
        new("Gabriela Luna", new DateTime(1984, 1, 11), 990, 110),
        new("Elena Marquez", new DateTime(1999, 9, 16), 1500, 170),
        new("Carolina Beltran", new DateTime(1990, 5, 7), 2450, 230),
        new("Jimena Robles", new DateTime(1988, 11, 5), 3200, 290),
        new("Montserrat Aguilar", new DateTime(1993, 7, 25), 5400, 360)
    ];

    private sealed record DemoCustomerSpec(string Name, DateTime BirthDate, int TargetPositivePoints, int BonusPoints);

    private sealed class MutableClock : IDateTimeProvider
    {
        public MutableClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; set; }
        public DateTime Today => UtcNow.Date;
    }
}
