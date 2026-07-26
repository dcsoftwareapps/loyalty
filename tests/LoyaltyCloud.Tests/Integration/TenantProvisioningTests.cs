extern alias AdminApp;

using System.Security.Claims;
using AdminApp::LoyaltyCloud.Admin.Auth;
using LoyaltyCloud.Application;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Customers.Commands.JoinCustomer;
using LoyaltyCloud.Application.Provisioning;
using LoyaltyCloud.Application.SuperAdmin.Commands.DeleteTenant;
using LoyaltyCloud.Common.Constants;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class TenantProvisioningTests
{
    private const string AdminPassword = "Provision123!";

    [Fact]
    [Trait("Category", "TenantProvisioning")]
    public async Task Provisioning_creates_complete_trial_tenant()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();

        var result = await env.ProvisionAsync("beauty-room", "Beauty Room", "owner", AdminPassword);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal("beauty-room", result.Value.TenantSlug);
        Assert.NotEqual(Guid.Empty, result.Value.TenantId);
        Assert.NotEqual(Guid.Empty, result.Value.AdminUserId);
        Assert.Equal(TenantSubscriptionStatus.Trial.ToString(), result.Value.SubscriptionStatus);

        var row = await env.PlatformReadAsync(async db => await db.Tenants
            .Where(t => t.Id == result.Value.TenantId)
            .Select(t => new
            {
                Tenant = t,
                Branding = t.Branding!,
                Subscription = t.Subscription!,
                Admin = db.TenantAdminUsers.IgnoreQueryFilters().Single(u => u.TenantId == t.Id),
                ConfigCount = db.ProgramConfigs.IgnoreQueryFilters().Count(c => c.TenantId == t.Id),
                LevelCount = db.TenantLoyaltyLevels.IgnoreQueryFilters().Count(l => l.TenantId == t.Id)
            })
            .SingleAsync());

        Assert.Equal("Beauty Room", row.Tenant.DisplayName);
        Assert.True(row.Tenant.IsActive);
        Assert.Equal("#111827", row.Branding.PrimaryColor);
        Assert.Equal("#F3F4F6", row.Branding.SecondaryColor);
        Assert.Equal(TenantSubscriptionStatus.Trial, row.Subscription.Status);
        Assert.Equal("trial", row.Subscription.PlanCode);
        Assert.NotNull(row.Subscription.CurrentPeriodStart);
        Assert.NotNull(row.Subscription.CurrentPeriodEnd);
        Assert.True(row.Subscription.IsOperational(DateTime.UtcNow));
        Assert.Equal("owner", row.Admin.Username);
        Assert.Equal("OWNER", row.Admin.NormalizedUsername);
        Assert.True(row.Admin.IsActive);
        Assert.NotEqual(AdminPassword, row.Admin.PasswordHash);
        Assert.DoesNotContain(AdminPassword, row.Admin.PasswordHash, StringComparison.Ordinal);
        Assert.Equal(TenantProvisioningDefaults.ProgramConfigRows.Count, row.ConfigCount);
        Assert.Equal(TenantProvisioningDefaults.LoyaltyLevels.Count, row.LevelCount);
    }

    [Fact]
    [Trait("Category", "TenantProvisioning")]
    [Trait("Category", "MTLevel1")]
    public async Task Provisioning_creates_default_loyalty_levels_for_new_tenant()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();

        var result = await env.ProvisionAsync("levels-spa", "Levels Spa", "owner", AdminPassword);

        Assert.True(result.IsSuccess, result.Error);

        var levels = await env.PlatformReadAsync(async db => await db.TenantLoyaltyLevels
            .IgnoreQueryFilters()
            .Where(level => level.TenantId == result.Value.TenantId)
            .OrderBy(level => level.SortOrder)
            .Select(level => new { level.Name, level.NormalizedName, level.Threshold, level.SortOrder, level.IsActive })
            .ToListAsync());

        Assert.Collection(
            levels,
            level =>
            {
                Assert.Equal(LoyaltyConstants.Levels.Mist, level.Name);
                Assert.Equal("MIST", level.NormalizedName);
                Assert.Equal(LoyaltyConstants.Defaults.LevelMistMin, level.Threshold);
                Assert.Equal(1, level.SortOrder);
                Assert.True(level.IsActive);
            },
            level =>
            {
                Assert.Equal(LoyaltyConstants.Levels.Glow, level.Name);
                Assert.Equal("GLOW", level.NormalizedName);
                Assert.Equal(LoyaltyConstants.Defaults.LevelGlowMin, level.Threshold);
                Assert.Equal(2, level.SortOrder);
                Assert.True(level.IsActive);
            },
            level =>
            {
                Assert.Equal(LoyaltyConstants.Levels.Radiance, level.Name);
                Assert.Equal("RADIANCE", level.NormalizedName);
                Assert.Equal(LoyaltyConstants.Defaults.LevelRadianceMin, level.Threshold);
                Assert.Equal(3, level.SortOrder);
                Assert.True(level.IsActive);
            });
    }

    [Fact]
    [Trait("Category", "TenantProvisioning")]
    public async Task Provisioned_admin_can_authenticate()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();
        await env.ProvisionAsync("spa-admin", "Spa Admin", "owner", AdminPassword);

        var login = await env.SignInAsync("spa-admin", "owner", AdminPassword);

        Assert.Equal(AdminLoginResult.Success, login);
    }

    [Fact]
    [Trait("Category", "TenantProvisioning")]
    public async Task Join_works_for_provisioned_tenant_and_is_isolated()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();
        var provisioned = await env.ProvisionAsync("skin-lab", "Skin Lab", "owner", AdminPassword);

        var join = await env.JoinAsync(provisioned.Value.TenantId, "skin-lab", "Ana", "Tenant", "6465550001");

        Assert.True(join.IsSuccess, join.Error);
        Assert.StartsWith("KB-", join.Value.SerialNumber, StringComparison.Ordinal);

        var tenantId = await env.PlatformReadAsync(async db => await db.Customers
            .IgnoreQueryFilters()
            .Where(c => c.NormalizedPhone == "6465550001")
            .Select(c => c.TenantId)
            .SingleAsync());
        Assert.Equal(provisioned.Value.TenantId, tenantId);
        Assert.NotEqual(TenantSeed.KBeautyTenantId, tenantId);
    }

    [Fact]
    [Trait("Category", "TenantProvisioning")]
    public async Task Duplicate_slug_fails_without_upsert()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();
        var first = await env.ProvisionAsync("duplicate-spa", "Duplicate Spa", "owner", AdminPassword);
        var second = await env.ProvisionAsync("duplicate-spa", "Duplicate Spa 2", "owner2", AdminPassword);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsFailure);
        Assert.Equal("El identificador del negocio ya está en uso.", second.Error);

        var count = await env.PlatformReadAsync(db => db.Tenants.CountAsync(t => t.Slug == "duplicate-spa"));
        Assert.Equal(1, count);
    }

    [Theory]
    [Trait("Category", "TenantProvisioning")]
    [InlineData("../kbeauty")]
    [InlineData("bella_salon")]
    [InlineData("bella/salon")]
    [InlineData("Bella Salon")]
    public async Task Invalid_slug_fails(string slug)
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();

        var result = await env.ProvisionAsync(slug, "Invalid Slug", "owner", AdminPassword);

        Assert.True(result.IsFailure);
    }

    [Fact]
    [Trait("Category", "TenantProvisioning")]
    public async Task Short_password_fails_and_does_not_create_tenant()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();

        var result = await env.ProvisionAsync("short-pass", "Short Pass", "owner", "short");

        Assert.True(result.IsFailure);
        var exists = await env.PlatformReadAsync(db => db.Tenants.AnyAsync(t => t.Slug == "short-pass"));
        Assert.False(exists);
    }

    [Fact]
    [Trait("Category", "TenantProvisioning")]
    public async Task Dangerous_url_fails_and_does_not_create_tenant()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();

        var result = await env.ProvisionAsync(
            "bad-url",
            "Bad URL",
            "owner",
            AdminPassword,
            instagramUrl: "javascript:alert(1)");

        Assert.True(result.IsFailure);
        var exists = await env.PlatformReadAsync(db => db.Tenants.AnyAsync(t => t.Slug == "bad-url"));
        Assert.False(exists);
    }

    [Fact]
    [Trait("Category", "TenantProvisioning")]
    public async Task Invalid_colors_are_normalized_to_generic_defaults()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();

        var result = await env.ProvisionAsync(
            "color-defaults",
            "Color Defaults",
            "owner",
            AdminPassword,
            primaryColor: "bad",
            secondaryColor: "also-bad");

        Assert.True(result.IsSuccess, result.Error);
        var branding = await env.PlatformReadAsync(db => db.TenantBrandings.SingleAsync(b => b.TenantId == result.Value.TenantId));
        Assert.Equal("#111827", branding.PrimaryColor);
        Assert.Equal("#F3F4F6", branding.SecondaryColor);
    }

    [Fact]
    [Trait("Category", "TenantProvisioning")]
    public async Task Same_admin_username_can_exist_in_different_tenants()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();

        var first = await env.ProvisionAsync("same-owner-a", "Same Owner A", "owner", AdminPassword);
        var second = await env.ProvisionAsync("same-owner-b", "Same Owner B", "owner", AdminPassword);

        Assert.True(first.IsSuccess, first.Error);
        Assert.True(second.IsSuccess, second.Error);
    }

    [Fact]
    [Trait("Category", "TenantProvisioning")]
    public async Task Provisioning_does_not_require_default_tenant_configuration()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();

        var result = await env.ProvisionAsync("no-default", "No Default", "owner", AdminPassword);

        Assert.True(result.IsSuccess, result.Error);
    }

    [Fact]
    [Trait("Category", "TenantProvisioning")]
    [Trait("Category", "PlatformTenantDeletion")]
    public async Task Platform_admin_can_hard_delete_tenant_and_preserve_other_tenants()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();
        var target = await env.ProvisionAsync("delete-me", "Delete Me", "owner", AdminPassword);
        var other = await env.ProvisionAsync("keep-me", "Keep Me", "owner", AdminPassword);
        Assert.True(target.IsSuccess, target.Error);
        Assert.True(other.IsSuccess, other.Error);

        var joined = await env.JoinAsync(target.Value.TenantId, "delete-me", "Ana", "Delete", "6465559999");
        Assert.True(joined.IsSuccess, joined.Error);

        var delete = await env.DeleteTenantAsync(target.Value.TenantId, "delete-me");

        Assert.True(delete.IsSuccess, delete.Error);
        var counts = await env.PlatformReadAsync(async db => new
        {
            Tenants = await db.Tenants.IgnoreQueryFilters().CountAsync(t => t.Id == target.Value.TenantId),
            AdminUsers = await db.TenantAdminUsers.IgnoreQueryFilters().CountAsync(t => t.TenantId == target.Value.TenantId),
            Branding = await db.TenantBrandings.CountAsync(t => t.TenantId == target.Value.TenantId),
            Subscription = await db.TenantSubscriptions.CountAsync(t => t.TenantId == target.Value.TenantId),
            Configs = await db.ProgramConfigs.IgnoreQueryFilters().CountAsync(t => t.TenantId == target.Value.TenantId),
            Levels = await db.TenantLoyaltyLevels.IgnoreQueryFilters().CountAsync(t => t.TenantId == target.Value.TenantId),
            Customers = await db.Customers.IgnoreQueryFilters().CountAsync(t => t.TenantId == target.Value.TenantId),
            Cards = await db.LoyaltyCards.IgnoreQueryFilters().CountAsync(t => t.TenantId == target.Value.TenantId),
            OtherTenant = await db.Tenants.IgnoreQueryFilters().CountAsync(t => t.Id == other.Value.TenantId),
            OtherAdmin = await db.TenantAdminUsers.IgnoreQueryFilters().CountAsync(t => t.TenantId == other.Value.TenantId)
        });

        Assert.Equal(0, counts.Tenants);
        Assert.Equal(0, counts.AdminUsers);
        Assert.Equal(0, counts.Branding);
        Assert.Equal(0, counts.Subscription);
        Assert.Equal(0, counts.Configs);
        Assert.Equal(0, counts.Levels);
        Assert.Equal(0, counts.Customers);
        Assert.Equal(0, counts.Cards);
        Assert.Equal(1, counts.OtherTenant);
        Assert.Equal(1, counts.OtherAdmin);
    }

    [Fact]
    [Trait("Category", "TenantProvisioning")]
    [Trait("Category", "PlatformTenantDeletion")]
    public async Task Hard_delete_requires_exact_slug_confirmation()
    {
        await using var env = await ProvisioningTestEnvironment.CreateAsync();
        var tenant = await env.ProvisionAsync("exact-slug", "Exact Slug", "owner", AdminPassword);
        Assert.True(tenant.IsSuccess, tenant.Error);

        var delete = await env.DeleteTenantAsync(tenant.Value.TenantId, "EXACT-SLUG");

        Assert.True(delete.IsFailure);
        Assert.Equal("El slug de confirmacion no coincide.", delete.Error);
        var exists = await env.PlatformReadAsync(db => db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == tenant.Value.TenantId));
        Assert.True(exists);
    }

    private sealed class ProvisioningTestEnvironment : IAsyncDisposable
    {
        private readonly ServiceProvider _services;

        private ProvisioningTestEnvironment(ServiceProvider services)
        {
            _services = services;
        }

        public static async Task<ProvisioningTestEnvironment> CreateAsync()
        {
            var dbName = "LoyaltyCloud_MT3E_" + Guid.NewGuid().ToString("N");
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
                    ["Provisioning:TrialDays"] = "14"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddApplication();
            services.AddInfrastructure(configuration, new TestHostEnvironment());
            services.RemoveAll<IStorageService>();
            services.AddScoped<IStorageService, InMemoryStorageService>();
            services.Configure<AdminAuthOptions>(_ => { });
            services.AddScoped<AdminAuthService>();
            services
                .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie();

            var provider = services.BuildServiceProvider(validateScopes: true);
            var env = new ProvisioningTestEnvironment(provider);
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

        public async Task<Result<ProvisionTenantResult>> ProvisionAsync(
            string slug,
            string displayName,
            string adminUsername,
            string password,
            string? primaryColor = null,
            string? secondaryColor = null,
            string? instagramUrl = null)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ProvisionTenantCommand(
                slug,
                displayName,
                TimeZoneId: null,
                adminUsername,
                password,
                PrimaryColor: primaryColor,
                SecondaryColor: secondaryColor,
                InstagramUrl: instagramUrl));
        }

        public async Task<AdminLoginResult> SignInAsync(string tenantSlug, string username, string password)
        {
            using var scope = _services.CreateScope();
            var context = CreateHttpContext(scope.ServiceProvider);
            return await scope.ServiceProvider.GetRequiredService<AdminAuthService>()
                .TrySignInAsync(context, tenantSlug, username, password);
        }

        public async Task<Result<JoinCustomerResponse>> JoinAsync(
            Guid tenantId,
            string tenantSlug,
            string firstName,
            string lastName,
            string phone)
        {
            using var scope = _services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(tenantId, tenantSlug);
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new JoinCustomerCommand(firstName, lastName, phone));
        }

        public async Task<Result> DeleteTenantAsync(Guid tenantId, string confirmationSlug)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new DeleteTenantCommand(tenantId, confirmationSlug));
        }

        public async Task<T> PlatformReadAsync<T>(Func<AppDbContext, Task<T>> query)
        {
            using var scope = _services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }

        private async Task InitializeAsync()
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureDeletedAsync();
            await db.Database.MigrateAsync();
        }

        private static DefaultHttpContext CreateHttpContext(IServiceProvider services)
        {
            var context = new DefaultHttpContext
            {
                RequestServices = services
            };
            context.Request.Scheme = "https";
            context.Request.Host = new HostString("admin.test");
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

    private sealed class InMemoryStorageService : IStorageService
    {
        private readonly Dictionary<string, byte[]> _passes = new(StringComparer.OrdinalIgnoreCase);

        public Task<string> UploadPassAsync(string serialNumber, byte[] passBytes, CancellationToken ct = default)
        {
            _passes[serialNumber] = passBytes;
            return Task.FromResult($"https://test.local/passes/{serialNumber}.pkpass");
        }

        public Task<byte[]?> DownloadPassAsync(string serialNumber, CancellationToken ct = default)
        {
            _passes.TryGetValue(serialNumber, out var passBytes);
            return Task.FromResult(passBytes);
        }
    }
}
