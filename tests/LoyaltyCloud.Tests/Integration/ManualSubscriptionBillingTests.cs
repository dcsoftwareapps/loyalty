using LoyaltyCloud.Application;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Provisioning;
using LoyaltyCloud.Application.SuperAdmin.Commands.RecordManualSubscriptionPayment;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class ManualSubscriptionBillingTests
{
    private static readonly DateTime FixedNow = new(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Provisioning_creates_trial_with_paid_through_null()
    {
        await using var env = await BillingTestEnvironment.CreateAsync();

        var result = await env.ProvisionAsync("billing-spa", "Billing Spa");

        Assert.True(result.IsSuccess, result.Error);
        var subscription = await env.PlatformReadAsync(db =>
            db.TenantSubscriptions.SingleAsync(s => s.TenantId == result.Value.TenantId));
        Assert.Equal(TenantSubscriptionStatus.Trial, subscription.Status);
        Assert.Null(subscription.PaidThroughUtc);
        Assert.Equal(FixedNow.AddDays(14), subscription.CurrentPeriodEnd);
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public void Trial_operational_depends_on_trial_end()
    {
        var current = new TenantSubscription(Guid.NewGuid(), TenantSubscriptionStatus.Trial, "trial", FixedNow, FixedNow.AddDays(1));
        var expired = new TenantSubscription(Guid.NewGuid(), TenantSubscriptionStatus.Trial, "trial", FixedNow.AddDays(-10), FixedNow.AddTicks(-1));

        Assert.True(current.IsOperational(FixedNow));
        Assert.False(expired.IsOperational(FixedNow));
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Maintenance_suspends_expired_trial()
    {
        await using var env = await BillingTestEnvironment.CreateAsync();
        var tenantId = await env.AddTenantAsync("expired-trial", TenantSubscriptionStatus.Trial, trialEnd: FixedNow.AddDays(-1));

        var result = await env.RunMaintenanceAsync();

        Assert.Equal(1, result.TrialsSuspended);
        Assert.Equal(TenantSubscriptionStatus.Suspended, await env.GetStatusAsync(tenantId));
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Trial_payment_sets_active_from_now()
    {
        await using var env = await BillingTestEnvironment.CreateAsync();
        var tenantId = await env.AddTenantAsync("trial-pay", TenantSubscriptionStatus.Trial, trialEnd: FixedNow.AddDays(10));

        var result = await env.RecordPaymentAsync(tenantId, 1);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(TenantSubscriptionStatus.Active, await env.GetStatusAsync(tenantId));
        Assert.Equal(FixedNow.AddMonths(1), result.Value.PaidThroughUtc);
    }

    [Theory]
    [Trait("Category", "ManualSubscriptionBilling")]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(12)]
    public async Task Payment_months_calculate_with_add_months(int months)
    {
        await using var env = await BillingTestEnvironment.CreateAsync();
        var tenantId = await env.AddTenantAsync("months-" + months, TenantSubscriptionStatus.Suspended);

        var result = await env.RecordPaymentAsync(tenantId, months);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(FixedNow.AddMonths(months), result.Value.PaidThroughUtc);
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Early_payment_extends_from_existing_paid_through()
    {
        await using var env = await BillingTestEnvironment.CreateAsync();
        var paidThrough = FixedNow.AddDays(19);
        var tenantId = await env.AddTenantAsync("early-pay", TenantSubscriptionStatus.Active, paidThrough: paidThrough);

        var result = await env.RecordPaymentAsync(tenantId, 1);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(paidThrough.AddMonths(1), result.Value.PaidThroughUtc);
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Expired_payment_extends_from_now()
    {
        await using var env = await BillingTestEnvironment.CreateAsync();
        var tenantId = await env.AddTenantAsync("expired-pay", TenantSubscriptionStatus.Active, paidThrough: FixedNow.AddDays(-1));

        var result = await env.RecordPaymentAsync(tenantId, 1);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(FixedNow.AddMonths(1), result.Value.PaidThroughUtc);
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public void Active_operational_depends_on_paid_through_with_legacy_null_exception()
    {
        var current = new TenantSubscription(Guid.NewGuid(), TenantSubscriptionStatus.Active, "manual", paidThroughUtc: FixedNow.AddDays(1));
        var expired = new TenantSubscription(Guid.NewGuid(), TenantSubscriptionStatus.Active, "manual", paidThroughUtc: FixedNow);
        var legacy = new TenantSubscription(Guid.NewGuid(), TenantSubscriptionStatus.Active, "legacy");

        Assert.True(current.IsOperational(FixedNow));
        Assert.False(expired.IsOperational(FixedNow));
        Assert.True(legacy.IsOperational(FixedNow));
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Maintenance_moves_expired_active_to_past_due_with_grace()
    {
        await using var env = await BillingTestEnvironment.CreateAsync(graceDays: 7);
        var tenantId = await env.AddTenantAsync("active-expired", TenantSubscriptionStatus.Active, paidThrough: FixedNow.AddMinutes(-1));

        var result = await env.RunMaintenanceAsync();

        Assert.Equal(1, result.ActiveMovedToPastDue);
        var subscription = await env.GetSubscriptionAsync(tenantId);
        Assert.Equal(TenantSubscriptionStatus.PastDue, subscription.Status);
        Assert.Equal(FixedNow.AddDays(7), subscription.GracePeriodEndsAt);
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public void Past_due_operational_depends_on_grace()
    {
        var current = new TenantSubscription(Guid.NewGuid(), TenantSubscriptionStatus.PastDue, "manual", gracePeriodEndsAt: FixedNow.AddDays(1));
        var expired = new TenantSubscription(Guid.NewGuid(), TenantSubscriptionStatus.PastDue, "manual", gracePeriodEndsAt: FixedNow);

        Assert.True(current.IsOperational(FixedNow));
        Assert.False(expired.IsOperational(FixedNow));
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Maintenance_suspends_expired_grace()
    {
        await using var env = await BillingTestEnvironment.CreateAsync();
        var tenantId = await env.AddTenantAsync("grace-expired", TenantSubscriptionStatus.PastDue, graceEnd: FixedNow.AddTicks(-1));

        var result = await env.RunMaintenanceAsync();

        Assert.Equal(1, result.PastDueSuspended);
        Assert.Equal(TenantSubscriptionStatus.Suspended, await env.GetStatusAsync(tenantId));
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Past_due_payment_sets_active_and_clears_grace()
    {
        await using var env = await BillingTestEnvironment.CreateAsync();
        var tenantId = await env.AddTenantAsync("pastdue-pay", TenantSubscriptionStatus.PastDue, paidThrough: FixedNow.AddDays(-2), graceEnd: FixedNow.AddDays(3));

        var result = await env.RecordPaymentAsync(tenantId, 1);

        Assert.True(result.IsSuccess, result.Error);
        var subscription = await env.GetSubscriptionAsync(tenantId);
        Assert.Equal(TenantSubscriptionStatus.Active, subscription.Status);
        Assert.Null(subscription.GracePeriodEndsAt);
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Suspended_payment_sets_active()
    {
        await using var env = await BillingTestEnvironment.CreateAsync();
        var tenantId = await env.AddTenantAsync("suspended-pay", TenantSubscriptionStatus.Suspended);

        var result = await env.RecordPaymentAsync(tenantId, 1);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(TenantSubscriptionStatus.Active, await env.GetStatusAsync(tenantId));
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Cancelled_payment_is_rejected()
    {
        await using var env = await BillingTestEnvironment.CreateAsync();
        var tenantId = await env.AddTenantAsync("cancelled-pay", TenantSubscriptionStatus.Cancelled);

        var result = await env.RecordPaymentAsync(tenantId, 1);

        Assert.True(result.IsFailure);
        Assert.Contains("cancelada", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Maintenance_is_idempotent()
    {
        await using var env = await BillingTestEnvironment.CreateAsync();
        await env.AddTenantAsync("idempotent-active", TenantSubscriptionStatus.Active, paidThrough: FixedNow.AddTicks(-1));

        var first = await env.RunMaintenanceAsync();
        var second = await env.RunMaintenanceAsync();

        Assert.Equal(1, first.ActiveMovedToPastDue);
        Assert.Equal(0, second.ActiveMovedToPastDue);
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Grace_period_zero_works()
    {
        await using var env = await BillingTestEnvironment.CreateAsync(graceDays: 0);
        var tenantId = await env.AddTenantAsync("zero-grace", TenantSubscriptionStatus.Active, paidThrough: FixedNow.AddTicks(-1));

        await env.RunMaintenanceAsync();

        var subscription = await env.GetSubscriptionAsync(tenantId);
        Assert.Equal(TenantSubscriptionStatus.PastDue, subscription.Status);
        Assert.Equal(FixedNow, subscription.GracePeriodEndsAt);
        Assert.False(subscription.IsOperational(FixedNow));
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public void Add_months_handles_end_of_month()
    {
        var subscription = new TenantSubscription(Guid.NewGuid(), TenantSubscriptionStatus.Active, "manual", paidThroughUtc: new DateTime(2026, 1, 31, 12, 0, 0, DateTimeKind.Utc));

        var paidThrough = subscription.RecordManualPayment(1, new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 2, 28, 12, 0, 0, DateTimeKind.Utc), paidThrough);
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Super_admin_page_does_not_assign_status_directly()
    {
        var source = await File.ReadAllTextAsync(BillingTestEnvironment.ReadAdminPagePath("PlatformTenantDetail.razor"));

        Assert.DoesNotContain("@bind=\"tenant.Subscription.Status\"", source);
        Assert.DoesNotContain("id=\"subscription-status\"", source);
        Assert.Contains("RecordManualSubscriptionPaymentCommand", source);
    }

    private sealed class BillingTestEnvironment : IAsyncDisposable
    {
        private readonly ServiceProvider _services;

        private BillingTestEnvironment(ServiceProvider services)
        {
            _services = services;
        }

        public static async Task<BillingTestEnvironment> CreateAsync(int graceDays = 7)
        {
            var dbName = "LoyaltyCloud_MT3G_" + Guid.NewGuid().ToString("N");
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True;",
                    ["Azure:KeyVaultUri"] = "",
                    ["Azure:BlobStorage:ConnectionString"] = "UseDevelopmentStorage=true",
                    ["Apple:PassTypeIdentifier"] = "pass.com.kbeautymx.loyalty",
                    ["Apple:TeamIdentifier"] = "TESTTEAM01",
                    ["Apple:WebServiceURL"] = "https://test.local",
                    ["Apple:OrganizationName"] = "LoyaltyCloud Test",
                    ["Wallet:UseRealPassSigning"] = "false",
                    ["Wallet:UseRealApns"] = "false",
                    ["Provisioning:TrialDays"] = "14",
                    ["Billing:GracePeriodDays"] = graceDays.ToString()
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddApplication();
            services.AddInfrastructure(configuration, new TestHostEnvironment());
            services.RemoveAll<IDateTimeProvider>();
            services.AddSingleton<IDateTimeProvider>(new FixedClock(FixedNow));

            var provider = services.BuildServiceProvider(validateScopes: true);
            var env = new BillingTestEnvironment(provider);
            await env.InitializeAsync();
            return env;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                using var scope = _services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
            }
            finally
            {
                await _services.DisposeAsync();
            }
        }

        public async Task<LoyaltyCloud.Common.Results.Result<ProvisionTenantResult>> ProvisionAsync(string slug, string displayName)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ProvisionTenantCommand(
                slug,
                displayName,
                TimeZoneId: null,
                AdminUsername: "owner",
                AdminPassword: "Tenant123!",
                PrimaryColor: null,
                SecondaryColor: null,
                SupportPhone: null,
                WhatsAppUrl: null,
                InstagramUrl: null,
                TermsUrl: null));
        }

        public async Task<LoyaltyCloud.Common.Results.Result<RecordManualSubscriptionPaymentResult>> RecordPaymentAsync(Guid tenantId, int months)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RecordManualSubscriptionPaymentCommand(tenantId, months));
        }

        public async Task<SubscriptionMaintenanceResult> RunMaintenanceAsync()
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ISubscriptionMaintenanceService>().ProcessAsync();
        }

        public async Task<Guid> AddTenantAsync(
            string slug,
            TenantSubscriptionStatus status,
            DateTime? trialEnd = null,
            DateTime? paidThrough = null,
            DateTime? graceEnd = null)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenantId = Guid.NewGuid();
            db.Tenants.Add(new Tenant(tenantId, slug, slug.Replace("-", " "), "America/Tijuana", FixedNow));
            db.TenantBrandings.Add(new TenantBranding(tenantId));
            db.TenantSubscriptions.Add(new TenantSubscription(
                tenantId,
                status,
                "manual",
                currentPeriodStart: FixedNow.AddDays(-14),
                currentPeriodEnd: trialEnd,
                paidThroughUtc: paidThrough,
                gracePeriodEndsAt: graceEnd));
            await db.SaveChangesAsync();
            return tenantId;
        }

        public async Task<TenantSubscription> GetSubscriptionAsync(Guid tenantId)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AppDbContext>()
                .TenantSubscriptions
                .AsNoTracking()
                .SingleAsync(s => s.TenantId == tenantId);
        }

        public async Task<TenantSubscriptionStatus> GetStatusAsync(Guid tenantId)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AppDbContext>()
                .TenantSubscriptions
                .Where(s => s.TenantId == tenantId)
                .Select(s => s.Status)
                .SingleAsync();
        }

        public async Task<T> PlatformReadAsync<T>(Func<AppDbContext, Task<T>> query)
        {
            using var scope = _services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }

        public static string ReadAdminPagePath(string fileName) =>
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "src",
                "LoyaltyCloud.Admin",
                "Pages",
                fileName);

        private async Task InitializeAsync()
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureDeletedAsync();
            await db.Database.MigrateAsync();
        }
    }

    private sealed class FixedClock : IDateTimeProvider
    {
        public FixedClock(DateTime utcNow)
        {
            UtcNow = utcNow;
            Today = utcNow.Date;
        }

        public DateTime UtcNow { get; }
        public DateTime Today { get; }
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
