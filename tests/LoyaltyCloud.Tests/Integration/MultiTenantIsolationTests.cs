using LoyaltyCloud.Application;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Customers.Queries.GetCustomerBySerial;
using LoyaltyCloud.Application.Points.Commands.AddPoints;
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

        private async Task InitializeAsync()
        {
            using (var scope = _services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.EnsureDeletedAsync();
                await db.Database.MigrateAsync();
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
                    "development"));
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
