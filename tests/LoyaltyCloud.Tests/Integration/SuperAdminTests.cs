extern alias AdminApp;

using System.Security.Claims;
using AdminApp::LoyaltyCloud.Admin.Auth;
using LoyaltyCloud.Application;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Provisioning;
using LoyaltyCloud.Application.SuperAdmin.Commands.CancelTenant;
using LoyaltyCloud.Application.SuperAdmin.Commands.ExtendTenantTrial;
using LoyaltyCloud.Application.SuperAdmin.Commands.ReactivateTenant;
using LoyaltyCloud.Application.SuperAdmin.Commands.SuspendTenant;
using LoyaltyCloud.Application.SuperAdmin.Commands.UpdateTenantGracePeriod;
using LoyaltyCloud.Application.SuperAdmin.Queries.ListPlatformTenants;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using LoyaltyCloud.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class SuperAdminTests
{
    private static readonly Guid BellaTenantId = Guid.Parse("c1000000-0000-0000-0000-000000000001");
    private const string SuperUsername = "platform";
    private const string SuperPassword = "Platform123!";

    [Fact]
    [Trait("Category", "SuperAdmin")]
    public async Task Super_admin_valid_login_succeeds()
    {
        await using var env = await SuperAdminTestEnvironment.CreateAsync();

        var result = await env.SignInAsync(SuperUsername, SuperPassword);

        Assert.Equal(SuperAdminLoginResult.Success, result.Result);
        Assert.Contains("loyaltycloud.platform.auth=", result.SetCookieHeader);
    }

    [Fact]
    [Trait("Category", "SuperAdmin")]
    public async Task Wrong_password_fails_without_revealing_user()
    {
        await using var env = await SuperAdminTestEnvironment.CreateAsync();

        var result = await env.SignInAsync(SuperUsername, "wrong-password");

        Assert.Equal(SuperAdminLoginResult.InvalidCredentials, result.Result);
        Assert.Null(result.SetCookieHeader);
    }

    [Fact]
    [Trait("Category", "SuperAdmin")]
    public void Super_admin_principal_has_no_tenant_claims()
    {
        var principal = SuperAdminTestEnvironment.CreateSuperAdminPrincipal();

        Assert.Equal(SuperAdminAuthDefaults.Role, principal.FindFirstValue(ClaimTypes.Role));
        Assert.Null(principal.FindFirstValue(AdminClaimTypes.TenantId));
        Assert.Null(principal.FindFirstValue(AdminClaimTypes.TenantSlug));
    }

    [Fact]
    [Trait("Category", "SuperAdmin")]
    public void Platform_routes_require_super_admin_authentication()
    {
        var tenantsSource = SuperAdminTestEnvironment.ReadAdminPage("PlatformTenants.razor");
        var detailSource = SuperAdminTestEnvironment.ReadAdminPage("PlatformTenantDetail.razor");

        Assert.Contains("SuperAdminAuthDefaults.AuthenticationScheme", tenantsSource);
        Assert.Contains("SuperAdminAuthDefaults.Role", tenantsSource);
        Assert.Contains("SuperAdminAuthDefaults.AuthenticationScheme", detailSource);
        Assert.Contains("SuperAdminAuthDefaults.Role", detailSource);
    }

    [Fact]
    [Trait("Category", "SuperAdmin")]
    public async Task Tenant_context_remains_empty_in_platform_operations()
    {
        await using var env = await SuperAdminTestEnvironment.CreateAsync();

        await env.WithScopeAsync(async sp =>
        {
            await sp.GetRequiredService<ISender>().Send(new ListPlatformTenantsQuery());
            var tenantContext = sp.GetRequiredService<ITenantContext>();
            Assert.False(tenantContext.HasTenant);
        });
    }

    [Fact]
    [Trait("Category", "SuperAdmin")]
    public async Task List_returns_kbeauty_and_bella_salon()
    {
        await using var env = await SuperAdminTestEnvironment.CreateAsync();

        var tenants = await env.WithScopeAsync(sp => sp.GetRequiredService<ISender>().Send(new ListPlatformTenantsQuery()));

        Assert.Contains(tenants, t => t.Slug == TenantSeed.KBeautySlug);
        Assert.Contains(tenants, t => t.Slug == "bella-salon");
    }

    [Fact]
    [Trait("Category", "SuperAdmin")]
    public async Task Create_tenant_uses_provisioning_and_starts_trial()
    {
        await using var env = await SuperAdminTestEnvironment.CreateAsync();

        var result = await env.ProvisionAsync("new-spa", "New Spa");

        Assert.True(result.IsSuccess, result.Error);
        var subscription = await env.PlatformReadAsync(db =>
            db.TenantSubscriptions.SingleAsync(s => s.TenantId == result.Value.TenantId));
        Assert.Equal(TenantSubscriptionStatus.Trial, subscription.Status);
    }

    [Fact]
    [Trait("Category", "SuperAdmin")]
    public async Task Duplicate_slug_returns_business_error()
    {
        await using var env = await SuperAdminTestEnvironment.CreateAsync();
        await env.ProvisionAsync("duplicate-platform", "Duplicate Platform");

        var result = await env.ProvisionAsync("duplicate-platform", "Duplicate Platform 2");

        Assert.True(result.IsFailure);
        Assert.Contains("uso", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SuperPassword, result.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "SuperAdmin")]
    public async Task Suspend_tenant_changes_subscription_and_blocks_operational_status()
    {
        await using var env = await SuperAdminTestEnvironment.CreateAsync();

        var result = await env.WithScopeAsync(sp => sp.GetRequiredService<ISender>().Send(new SuspendTenantCommand(BellaTenantId)));

        Assert.True(result.IsSuccess, result.Error);
        var subscription = await env.PlatformReadAsync(db => db.TenantSubscriptions.SingleAsync(s => s.TenantId == BellaTenantId));
        Assert.Equal(TenantSubscriptionStatus.Suspended, subscription.Status);
        Assert.False(subscription.IsOperational(DateTime.UtcNow));
    }

    [Fact]
    [Trait("Category", "SuperAdmin")]
    public async Task Reactivate_sets_suspended_tenant_to_active()
    {
        await using var env = await SuperAdminTestEnvironment.CreateAsync();
        await env.WithScopeAsync(sp => sp.GetRequiredService<ISender>().Send(new SuspendTenantCommand(BellaTenantId)));

        var result = await env.WithScopeAsync(sp => sp.GetRequiredService<ISender>().Send(new ReactivateTenantCommand(BellaTenantId)));

        Assert.True(result.IsSuccess, result.Error);
        var status = await env.PlatformReadAsync(db => db.TenantSubscriptions.Where(s => s.TenantId == BellaTenantId).Select(s => s.Status).SingleAsync());
        Assert.Equal(TenantSubscriptionStatus.Active, status);
    }

    [Fact]
    [Trait("Category", "SuperAdmin")]
    public async Task Cancel_sets_subscription_without_deleting_tenant()
    {
        await using var env = await SuperAdminTestEnvironment.CreateAsync();

        var result = await env.WithScopeAsync(sp => sp.GetRequiredService<ISender>().Send(new CancelTenantCommand(BellaTenantId)));

        Assert.True(result.IsSuccess, result.Error);
        var row = await env.PlatformReadAsync(async db => new
        {
            TenantExists = await db.Tenants.AnyAsync(t => t.Id == BellaTenantId),
            Status = await db.TenantSubscriptions.Where(s => s.TenantId == BellaTenantId).Select(s => s.Status).SingleAsync()
        });
        Assert.True(row.TenantExists);
        Assert.Equal(TenantSubscriptionStatus.Cancelled, row.Status);
    }

    [Fact]
    [Trait("Category", "SuperAdmin")]
    public async Task Extend_trial_updates_period_end()
    {
        await using var env = await SuperAdminTestEnvironment.CreateAsync();
        var newEnd = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(45), DateTimeKind.Utc);

        var result = await env.WithScopeAsync(sp => sp.GetRequiredService<ISender>().Send(new ExtendTenantTrialCommand(BellaTenantId, newEnd)));

        Assert.True(result.IsSuccess, result.Error);
        var periodEnd = await env.PlatformReadAsync(db =>
            db.TenantSubscriptions.Where(s => s.TenantId == BellaTenantId).Select(s => s.CurrentPeriodEnd).SingleAsync());
        Assert.Equal(newEnd, periodEnd);
    }

    [Fact]
    [Trait("Category", "SuperAdmin")]
    public async Task Update_grace_period_works_for_past_due_tenant()
    {
        await using var env = await SuperAdminTestEnvironment.CreateAsync();
        await env.SetSubscriptionStatusAsync(BellaTenantId, TenantSubscriptionStatus.PastDue);
        var graceEnd = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(10), DateTimeKind.Utc);

        var result = await env.WithScopeAsync(sp => sp.GetRequiredService<ISender>().Send(new UpdateTenantGracePeriodCommand(BellaTenantId, graceEnd)));

        Assert.True(result.IsSuccess, result.Error);
        var saved = await env.PlatformReadAsync(db =>
            db.TenantSubscriptions.Where(s => s.TenantId == BellaTenantId).Select(s => s.GracePeriodEndsAt).SingleAsync());
        Assert.Equal(graceEnd, saved);
    }

    [Fact]
    [Trait("Category", "SuperAdmin")]
    public void Tenant_admin_cookie_does_not_grant_platform_role()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AdminClaimTypes.Subject, Guid.NewGuid().ToString()),
            new Claim(AdminClaimTypes.TenantId, TenantSeed.KBeautyTenantId.ToString()),
            new Claim(AdminClaimTypes.TenantSlug, TenantSeed.KBeautySlug),
            new Claim(AdminClaimTypes.Name, "owner")
        }, CookieAuthenticationDefaults.AuthenticationScheme, AdminClaimTypes.Name, ClaimTypes.Role));

        Assert.False(principal.IsInRole(SuperAdminAuthDefaults.Role));
    }

    [Fact]
    [Trait("Category", "SuperAdmin")]
    public async Task Super_admin_context_does_not_expose_tenant_owned_data()
    {
        await using var env = await SuperAdminTestEnvironment.CreateAsync();

        var count = await env.PlatformReadAsync(db => db.Customers.CountAsync());

        Assert.Equal(0, count);
    }

    private sealed class SuperAdminTestEnvironment : IAsyncDisposable
    {
        private readonly ServiceProvider _services;

        private SuperAdminTestEnvironment(ServiceProvider services)
        {
            _services = services;
        }

        public static async Task<SuperAdminTestEnvironment> CreateAsync()
        {
            var dbName = "LoyaltyCloud_MT3F_" + Guid.NewGuid().ToString("N");
            var passwordHash = new PasswordHashingService().HashPassword(SuperPassword);
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
                    ["SuperAdmin:Username"] = SuperUsername,
                    ["SuperAdmin:PasswordHash"] = passwordHash,
                    ["SuperAdmin:SessionHours"] = "8"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddApplication();
            services.AddInfrastructure(configuration, new TestHostEnvironment());
            services.Configure<SuperAdminAuthOptions>(configuration.GetSection(SuperAdminAuthOptions.SectionName));
            services.AddScoped<SuperAdminAuthService>();
            services
                .AddAuthentication(SuperAdminAuthDefaults.AuthenticationScheme)
                .AddCookie(SuperAdminAuthDefaults.AuthenticationScheme, options => options.Cookie.Name = "loyaltycloud.platform.auth");

            var provider = services.BuildServiceProvider(validateScopes: true);
            var env = new SuperAdminTestEnvironment(provider);
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

        public async Task<(SuperAdminLoginResult Result, string? SetCookieHeader)> SignInAsync(string username, string password)
        {
            using var scope = _services.CreateScope();
            var context = CreateHttpContext(scope.ServiceProvider);
            var result = await scope.ServiceProvider.GetRequiredService<SuperAdminAuthService>()
                .TrySignInAsync(context, username, password);
            return (result, context.Response.Headers.SetCookie.FirstOrDefault());
        }

        public async Task<LoyaltyCloud.Common.Results.Result<ProvisionTenantResult>> ProvisionAsync(string slug, string displayName)
        {
            return await WithScopeAsync(sp => sp.GetRequiredService<ISender>().Send(new ProvisionTenantCommand(
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
                TermsUrl: null)));
        }

        public async Task SetSubscriptionStatusAsync(Guid tenantId, TenantSubscriptionStatus status)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var subscription = await db.TenantSubscriptions.SingleAsync(s => s.TenantId == tenantId);
            db.Entry(subscription).Property(nameof(TenantSubscription.Status)).CurrentValue = status;
            await db.SaveChangesAsync();
        }

        public async Task<T> WithScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
        {
            using var scope = _services.CreateScope();
            return await action(scope.ServiceProvider);
        }

        public async Task WithScopeAsync(Func<IServiceProvider, Task> action)
        {
            using var scope = _services.CreateScope();
            await action(scope.ServiceProvider);
        }

        public async Task<T> PlatformReadAsync<T>(Func<AppDbContext, Task<T>> query)
        {
            using var scope = _services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }

        public static ClaimsPrincipal CreateSuperAdminPrincipal()
        {
            var claims = new[]
            {
                new Claim("sub", "platform"),
                new Claim(ClaimTypes.NameIdentifier, "platform"),
                new Claim(ClaimTypes.Name, SuperUsername),
                new Claim(ClaimTypes.Role, SuperAdminAuthDefaults.Role)
            };
            return new ClaimsPrincipal(new ClaimsIdentity(
                claims,
                SuperAdminAuthDefaults.AuthenticationScheme,
                ClaimTypes.Name,
                ClaimTypes.Role));
        }

        public static string ReadAdminPage(string fileName)
        {
            var path = Path.Combine(
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
            return File.ReadAllText(path);
        }

        private async Task InitializeAsync()
        {
            using (var scope = _services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.EnsureDeletedAsync();
                await db.Database.MigrateAsync();
            }

            await SeedBellaTenantAsync();
            await SeedCustomerAsync();
        }

        private async Task SeedBellaTenantAsync()
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Tenants.Add(new Tenant(BellaTenantId, "bella-salon", "Bella Salon", "America/Tijuana", DateTime.UtcNow));
            db.TenantBrandings.Add(new TenantBranding(BellaTenantId, primaryColor: "#8B5CF6", secondaryColor: "#F5D0FE"));
            db.TenantSubscriptions.Add(new TenantSubscription(
                BellaTenantId,
                TenantSubscriptionStatus.Trial,
                "trial",
                DateTime.UtcNow,
                DateTime.UtcNow.AddDays(14)));
            await db.SaveChangesAsync();
        }

        private async Task SeedCustomerAsync()
        {
            using var scope = _services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Customers.Add(new Customer(
                Guid.Parse("c1000000-0000-0000-0000-000000000101"),
                TenantSeed.KBeautyTenantId,
                "Hidden Customer",
                "hidden@test.local",
                new DateTime(1990, 1, 1),
                DateTime.UtcNow,
                "6460000000"));
            await db.SaveChangesAsync();
        }

        private static DefaultHttpContext CreateHttpContext(IServiceProvider services)
        {
            var context = new DefaultHttpContext
            {
                RequestServices = services
            };
            context.Request.Scheme = "https";
            context.Request.Host = new HostString("platform.test");
            return context;
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
