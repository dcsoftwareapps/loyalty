using LoyaltyCloud.Application;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Provisioning;
using LoyaltyCloud.Application.SuperAdmin.Commands.RecordManualSubscriptionPayment;
using LoyaltyCloud.Application.SuperAdmin.Commands.ReactivateTenant;
using LoyaltyCloud.Application.SuperAdmin.Commands.SuspendTenant;
using LoyaltyCloud.Application.SuperAdmin.Queries.GetPlatformTenant;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure;
using LoyaltyCloud.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class SubscriptionHardeningTests
{
    private static readonly DateTime FixedNow = new(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Category", "SubscriptionHardening")]
    public void Active_without_paid_through_is_not_operational()
    {
        var subscription = new TenantSubscription(Guid.NewGuid(), TenantSubscriptionStatus.Active, "legacy");

        Assert.False(subscription.IsOperational(FixedNow));
    }

    [Fact]
    [Trait("Category", "SubscriptionHardening")]
    public void Active_with_future_paid_through_is_operational()
    {
        var subscription = new TenantSubscription(Guid.NewGuid(), TenantSubscriptionStatus.Active, "manual", paidThroughUtc: FixedNow.AddDays(1));

        Assert.True(subscription.IsOperational(FixedNow));
    }

    [Fact]
    [Trait("Category", "SubscriptionHardening")]
    public async Task Expired_trial_suspends_with_trial_expired_reason()
    {
        await using var env = await HardeningEnvironment.CreateAsync();
        var tenantId = await env.AddTenantAsync("trial-expired", TenantSubscriptionStatus.Trial, trialEnd: FixedNow.AddTicks(-1));

        await env.RunMaintenanceAsync();

        var subscription = await env.GetSubscriptionAsync(tenantId);
        Assert.Equal(TenantSubscriptionStatus.Suspended, subscription.Status);
        Assert.Equal(TenantSuspensionReason.TrialExpired, subscription.SuspensionReason);
    }

    [Fact]
    [Trait("Category", "SubscriptionHardening")]
    public async Task Expired_past_due_suspends_with_payment_reason()
    {
        await using var env = await HardeningEnvironment.CreateAsync();
        var tenantId = await env.AddTenantAsync("past-due-expired", TenantSubscriptionStatus.PastDue, graceEnd: FixedNow.AddTicks(-1));

        await env.RunMaintenanceAsync();

        var subscription = await env.GetSubscriptionAsync(tenantId);
        Assert.Equal(TenantSubscriptionStatus.Suspended, subscription.Status);
        Assert.Equal(TenantSuspensionReason.PaymentPastDue, subscription.SuspensionReason);
    }

    [Fact]
    [Trait("Category", "SubscriptionHardening")]
    public async Task Manual_suspend_sets_administrative_reason()
    {
        await using var env = await HardeningEnvironment.CreateAsync();
        var tenantId = await env.AddTenantAsync("manual-suspend", TenantSubscriptionStatus.Active, paidThrough: FixedNow.AddDays(10));

        var result = await env.SendAsync(new SuspendTenantCommand(tenantId));

        Assert.True(result.IsSuccess, result.Error);
        var subscription = await env.GetSubscriptionAsync(tenantId);
        Assert.Equal(TenantSubscriptionStatus.Suspended, subscription.Status);
        Assert.Equal(TenantSuspensionReason.Administrative, subscription.SuspensionReason);
    }

    [Theory]
    [Trait("Category", "SubscriptionHardening")]
    [InlineData(TenantSuspensionReason.PaymentPastDue)]
    [InlineData(TenantSuspensionReason.TrialExpired)]
    public async Task Payment_reactivates_billing_suspensions(TenantSuspensionReason reason)
    {
        await using var env = await HardeningEnvironment.CreateAsync();
        var tenantId = await env.AddTenantAsync("pay-" + reason.ToString().ToLowerInvariant(), TenantSubscriptionStatus.Suspended, suspensionReason: reason);

        var result = await env.RecordPaymentAsync(tenantId, 1);

        Assert.True(result.IsSuccess, result.Error);
        var subscription = await env.GetSubscriptionAsync(tenantId);
        Assert.Equal(TenantSubscriptionStatus.Active, subscription.Status);
        Assert.Null(subscription.SuspensionReason);
    }

    [Fact]
    [Trait("Category", "SubscriptionHardening")]
    public async Task Payment_does_not_reactivate_administrative_suspension()
    {
        await using var env = await HardeningEnvironment.CreateAsync();
        var tenantId = await env.AddTenantAsync("admin-suspended", TenantSubscriptionStatus.Suspended, paidThrough: FixedNow.AddDays(10), suspensionReason: TenantSuspensionReason.Administrative);

        var result = await env.RecordPaymentAsync(tenantId, 1);

        Assert.True(result.IsFailure);
        Assert.Contains("administrativamente", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "SubscriptionHardening")]
    public async Task Payment_does_not_reactivate_legacy_suspended_without_reason()
    {
        await using var env = await HardeningEnvironment.CreateAsync();
        var tenantId = await env.AddTenantAsync("legacy-suspended", TenantSubscriptionStatus.Suspended);

        var result = await env.RecordPaymentAsync(tenantId, 1);

        Assert.True(result.IsFailure);
        Assert.Contains("legacy", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "SubscriptionHardening")]
    public async Task Cancelled_still_rejects_payment()
    {
        await using var env = await HardeningEnvironment.CreateAsync();
        var tenantId = await env.AddTenantAsync("cancelled-hardening", TenantSubscriptionStatus.Cancelled);

        var result = await env.RecordPaymentAsync(tenantId, 1);

        Assert.True(result.IsFailure);
        Assert.Contains("cancelada", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "SubscriptionHardening")]
    public async Task Administrative_reactivation_does_not_create_active_without_paid_through()
    {
        await using var env = await HardeningEnvironment.CreateAsync();
        var tenantId = await env.AddTenantAsync("admin-no-paid", TenantSubscriptionStatus.Suspended, suspensionReason: TenantSuspensionReason.Administrative);

        var result = await env.SendAsync(new ReactivateTenantCommand(tenantId));

        Assert.True(result.IsFailure);
        Assert.Equal(TenantSubscriptionStatus.Suspended, await env.GetStatusAsync(tenantId));
    }

    [Fact]
    [Trait("Category", "SubscriptionHardening")]
    public async Task Administrative_reactivation_with_paid_through_clears_reason()
    {
        await using var env = await HardeningEnvironment.CreateAsync();
        var tenantId = await env.AddTenantAsync("admin-paid", TenantSubscriptionStatus.Suspended, paidThrough: FixedNow.AddDays(10), suspensionReason: TenantSuspensionReason.Administrative);

        var result = await env.SendAsync(new ReactivateTenantCommand(tenantId));

        Assert.True(result.IsSuccess, result.Error);
        var subscription = await env.GetSubscriptionAsync(tenantId);
        Assert.Equal(TenantSubscriptionStatus.Active, subscription.Status);
        Assert.Null(subscription.SuspensionReason);
    }

    [Fact]
    [Trait("Category", "SubscriptionHardening")]
    public async Task Platform_query_returns_suspension_reason()
    {
        await using var env = await HardeningEnvironment.CreateAsync();
        var tenantId = await env.AddTenantAsync("query-reason", TenantSubscriptionStatus.Suspended, suspensionReason: TenantSuspensionReason.PaymentPastDue);

        var dto = await env.SendAsync(new GetPlatformTenantQuery(tenantId));

        Assert.NotNull(dto);
        Assert.Equal(nameof(TenantSuspensionReason.PaymentPastDue), dto!.Subscription!.SuspensionReason);
    }

    [Fact]
    [Trait("Category", "SubscriptionHardening")]
    public async Task Maintenance_is_idempotent_after_setting_reason()
    {
        await using var env = await HardeningEnvironment.CreateAsync();
        await env.AddTenantAsync("idempotent-hardening", TenantSubscriptionStatus.PastDue, graceEnd: FixedNow.AddTicks(-1));

        var first = await env.RunMaintenanceAsync();
        var second = await env.RunMaintenanceAsync();

        Assert.Equal(1, first.PastDueSuspended);
        Assert.Equal(0, second.PastDueSuspended);
    }

    [Fact]
    [Trait("Category", "SubscriptionHardening")]
    public async Task Provisioning_still_creates_trial()
    {
        await using var env = await HardeningEnvironment.CreateAsync();

        var result = await env.ProvisionAsync("hardening-provision", "Hardening Provision");

        Assert.True(result.IsSuccess, result.Error);
        var subscription = await env.GetSubscriptionAsync(result.Value.TenantId);
        Assert.Equal(TenantSubscriptionStatus.Trial, subscription.Status);
        Assert.Null(subscription.PaidThroughUtc);
        Assert.Null(subscription.SuspensionReason);
    }

    [Fact]
    [Trait("Category", "SubscriptionHardening")]
    public void Payment_preview_calculates_from_now()
    {
        var paidThrough = TenantSubscription.CalculateManualPaymentPaidThrough(null, 1, FixedNow);

        Assert.Equal(FixedNow.AddMonths(1), paidThrough);
    }

    [Fact]
    [Trait("Category", "SubscriptionHardening")]
    public void Payment_preview_calculates_from_future_paid_through()
    {
        var current = FixedNow.AddDays(20);

        var paidThrough = TenantSubscription.CalculateManualPaymentPaidThrough(current, 3, FixedNow);

        Assert.Equal(current.AddMonths(3), paidThrough);
    }

    [Fact]
    [Trait("Category", "SubscriptionHardening")]
    public void Is_operational_does_not_depend_on_maintenance()
    {
        var expiredActive = new TenantSubscription(Guid.NewGuid(), TenantSubscriptionStatus.Active, "manual", paidThroughUtc: FixedNow.AddTicks(-1));

        Assert.False(expiredActive.IsOperational(FixedNow));
    }

    [Fact]
    [Trait("Category", "SubscriptionHardening")]
    public void Administrative_suspension_blocks_even_with_future_paid_through()
    {
        var subscription = new TenantSubscription(
            Guid.NewGuid(),
            TenantSubscriptionStatus.Suspended,
            "manual",
            paidThroughUtc: FixedNow.AddDays(30),
            suspensionReason: TenantSuspensionReason.Administrative);

        Assert.False(subscription.IsOperational(FixedNow));
    }

    [Fact]
    [Trait("Category", "SubscriptionHardening")]
    public async Task Invalid_transition_fails()
    {
        await using var env = await HardeningEnvironment.CreateAsync();
        var tenantId = await env.AddTenantAsync("invalid-reactivate", TenantSubscriptionStatus.PastDue, graceEnd: FixedNow.AddDays(1));

        var result = await env.SendAsync(new ReactivateTenantCommand(tenantId));

        Assert.True(result.IsFailure);
    }

    private sealed class HardeningEnvironment : IAsyncDisposable
    {
        private readonly ServiceProvider _services;

        private HardeningEnvironment(ServiceProvider services)
        {
            _services = services;
        }

        public static async Task<HardeningEnvironment> CreateAsync()
        {
            var dbName = "LoyaltyCloud_MT3H_" + Guid.NewGuid().ToString("N");
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
                    ["Billing:GracePeriodDays"] = "7"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddApplication();
            services.AddInfrastructure(configuration, new TestHostEnvironment());
            services.RemoveAll<IDateTimeProvider>();
            services.AddSingleton<IDateTimeProvider>(new FixedClock(FixedNow));

            var provider = services.BuildServiceProvider(validateScopes: true);
            var env = new HardeningEnvironment(provider);
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

        public async Task<Guid> AddTenantAsync(
            string slug,
            TenantSubscriptionStatus status,
            DateTime? trialEnd = null,
            DateTime? paidThrough = null,
            DateTime? graceEnd = null,
            TenantSuspensionReason? suspensionReason = null)
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
                gracePeriodEndsAt: graceEnd,
                suspensionReason: suspensionReason));
            await db.SaveChangesAsync();
            return tenantId;
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

        public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
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
