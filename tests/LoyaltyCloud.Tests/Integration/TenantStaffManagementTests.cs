extern alias AdminApp;

using AdminApp::LoyaltyCloud.Admin.Auth;
using AdminApp::LoyaltyCloud.Admin.Services;
using LoyaltyCloud.Application;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class TenantStaffManagementTests
{
    private static readonly Guid BellaTenantId = Guid.Parse("c1000000-0000-0000-0000-000000000001");
    private static readonly Guid KBeautyAdminId = Guid.Parse("c1000000-0000-0000-0000-000000000101");
    private static readonly Guid BellaAdminId = Guid.Parse("c1000000-0000-0000-0000-000000000102");
    private const string BellaSlug = "bella-staff";
    private const string AdminPassword = "AdminStaff123!";

    [Fact]
    [Trait("Category", "StaffManagement")]
    public async Task Tenant_admin_can_create_cashier_with_normalized_hashed_password()
    {
        await using var env = await StaffTestEnvironment.CreateAsync();

        var created = await env.CreateUserAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, "Caja Uno", "Cashier123!", TenantUserRole.Cashier);

        Assert.True(created.IsSuccess, created.Error);
        Assert.Equal(TenantUserRole.Cashier, created.Value.Role);
        Assert.True(created.Value.IsActive);
        Assert.Equal("CAJA UNO", created.Value.NormalizedUsername);

        var stored = await env.GetUserAsync(TenantSeed.KBeautyTenantId, "Caja Uno");
        Assert.NotNull(stored);
        Assert.NotEqual("Cashier123!", stored!.PasswordHash);
        Assert.True(await env.VerifyPasswordAsync(stored.Id, "Cashier123!"));
    }

    [Fact]
    [Trait("Category", "StaffManagement")]
    public async Task Tenant_admin_can_create_admin_user()
    {
        await using var env = await StaffTestEnvironment.CreateAsync();

        var created = await env.CreateUserAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, "manager", "Manager123!", TenantUserRole.Admin);

        Assert.True(created.IsSuccess, created.Error);
        Assert.Equal(TenantUserRole.Admin, created.Value.Role);
        var stored = await env.GetUserAsync(TenantSeed.KBeautyTenantId, "manager");
        Assert.Equal(TenantUserRole.Admin, stored!.Role);
    }

    [Fact]
    [Trait("Category", "StaffManagement")]
    public async Task Staff_list_only_returns_current_tenant_users()
    {
        await using var env = await StaffTestEnvironment.CreateAsync();
        await env.CreateUserAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, "kbeauty-cashier", "Cashier123!", TenantUserRole.Cashier);
        await env.CreateUserAsync(BellaTenantId, BellaSlug, "bella-cashier", "Cashier123!", TenantUserRole.Cashier);

        var kbeautyUsers = await env.WithTenantAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, async sp =>
            await sp.GetRequiredService<TenantStaffService>().ListAsync());

        Assert.Contains(kbeautyUsers, user => user.Username == "kbeauty-cashier");
        Assert.DoesNotContain(kbeautyUsers, user => user.Username == "bella-cashier");
    }

    [Fact]
    [Trait("Category", "StaffManagement")]
    public async Task Duplicate_username_in_same_tenant_is_rejected_but_other_tenant_is_allowed()
    {
        await using var env = await StaffTestEnvironment.CreateAsync();

        var first = await env.CreateUserAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, "shared", "Cashier123!", TenantUserRole.Cashier);
        var duplicate = await env.CreateUserAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, "SHARED", "Cashier123!", TenantUserRole.Cashier);
        var otherTenant = await env.CreateUserAsync(BellaTenantId, BellaSlug, "shared", "Cashier123!", TenantUserRole.Cashier);

        Assert.True(first.IsSuccess, first.Error);
        Assert.True(duplicate.IsFailure);
        Assert.Contains("Ya existe", duplicate.Error);
        Assert.True(otherTenant.IsSuccess, otherTenant.Error);
    }

    [Fact]
    [Trait("Category", "StaffManagement")]
    public async Task Missing_or_short_password_is_rejected()
    {
        await using var env = await StaffTestEnvironment.CreateAsync();

        var missing = await env.CreateUserAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, "missing-password", "", TenantUserRole.Cashier);
        var shortPassword = await env.CreateUserAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, "short-password", "short", TenantUserRole.Cashier);

        Assert.True(missing.IsFailure);
        Assert.Contains("Password requerido", missing.Error);
        Assert.True(shortPassword.IsFailure);
        Assert.Contains("al menos 8", shortPassword.Error);
    }

    [Fact]
    [Trait("Category", "StaffManagement")]
    public async Task Cashier_can_be_deactivated_and_inactive_user_cannot_login()
    {
        await using var env = await StaffTestEnvironment.CreateAsync();
        var created = await env.CreateUserAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, "cashier-off", "Cashier123!", TenantUserRole.Cashier);

        var deactivate = await env.WithTenantAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, async sp =>
            await sp.GetRequiredService<TenantStaffService>().SetActiveAsync(created.Value.Id, false));
        var login = await env.SignInAsync(TenantSeed.KBeautySlug, "cashier-off", "Cashier123!");

        Assert.True(deactivate.IsSuccess, deactivate.Error);
        Assert.Equal(AdminLoginResult.InvalidCredentials, login);
    }

    [Fact]
    [Trait("Category", "StaffManagement")]
    public async Task Cannot_modify_user_from_another_tenant()
    {
        await using var env = await StaffTestEnvironment.CreateAsync();
        var bellaUser = await env.CreateUserAsync(BellaTenantId, BellaSlug, "bella-cashier", "Cashier123!", TenantUserRole.Cashier);

        var result = await env.WithTenantAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, async sp =>
            await sp.GetRequiredService<TenantStaffService>().SetActiveAsync(bellaUser.Value.Id, false));

        Assert.True(result.IsFailure);
        Assert.Equal("Usuario no encontrado.", result.Error);
        Assert.True((await env.GetUserAsync(BellaTenantId, "bella-cashier"))!.IsActive);
    }

    [Fact]
    [Trait("Category", "StaffManagement")]
    public async Task Cannot_deactivate_last_active_admin()
    {
        await using var env = await StaffTestEnvironment.CreateAsync();

        var result = await env.WithTenantAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, async sp =>
            await sp.GetRequiredService<TenantStaffService>().SetActiveAsync(KBeautyAdminId, false));

        Assert.True(result.IsFailure);
        Assert.Contains("ultimo Admin activo", result.Error);
        Assert.True((await env.GetUserByIdAsync(KBeautyAdminId))!.IsActive);
    }

    [Fact]
    [Trait("Category", "StaffManagement")]
    public async Task Reset_password_updates_hash_and_new_password_can_login()
    {
        await using var env = await StaffTestEnvironment.CreateAsync();
        var created = await env.CreateUserAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, "reset-me", "OldPass123!", TenantUserRole.Admin);

        var before = (await env.GetUserByIdAsync(created.Value.Id))!.PasswordHash;
        var reset = await env.WithTenantAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, async sp =>
            await sp.GetRequiredService<TenantStaffService>().ResetPasswordAsync(created.Value.Id, "NewPass123!"));
        var after = (await env.GetUserByIdAsync(created.Value.Id))!.PasswordHash;
        var oldLogin = await env.SignInAsync(TenantSeed.KBeautySlug, "reset-me", "OldPass123!");
        var newLogin = await env.SignInAsync(TenantSeed.KBeautySlug, "reset-me", "NewPass123!");

        Assert.True(reset.IsSuccess, reset.Error);
        Assert.NotEqual(before, after);
        Assert.Equal(AdminLoginResult.InvalidCredentials, oldLogin);
        Assert.Equal(AdminLoginResult.Success, newLogin);
    }

    private sealed class StaffTestEnvironment : IAsyncDisposable
    {
        private readonly ServiceProvider _services;

        private StaffTestEnvironment(ServiceProvider services) => _services = services;

        public static async Task<StaffTestEnvironment> CreateAsync()
        {
            var dbName = "LoyaltyCloudStaffManagement-" + Guid.NewGuid().ToString("N");
            var connectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True;";
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                    ["Azure:KeyVaultUri"] = "",
                    ["Azure:BlobStorage:ConnectionString"] = "UseDevelopmentStorage=true",
                    ["Apple:PassTypeIdentifier"] = "pass.com.kbeautymx.loyalty",
                    ["Apple:TeamIdentifier"] = "TESTTEAM01",
                    ["Apple:WebServiceURL"] = "https://test.local",
                    ["Apple:OrganizationName"] = "LoyaltyCloud Test",
                    ["Wallet:UseRealPassSigning"] = "false",
                    ["Wallet:UseRealApns"] = "false",
                    ["Admin:Auth:SessionHours"] = "168"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddApplication();
            services.AddInfrastructure(configuration, new TestHostEnvironment());
            services.Configure<AdminAuthOptions>(configuration.GetSection(AdminAuthOptions.SectionName));
            services.AddScoped<AdminAuthService>();
            services.AddScoped<TenantStaffService>();
            services
                .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options => options.Cookie.Name = "loyaltycloud.admin.auth");

            var provider = services.BuildServiceProvider(validateScopes: true);
            var env = new StaffTestEnvironment(provider);
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

        public async Task<T> WithTenantAsync<T>(Guid tenantId, string tenantSlug, Func<IServiceProvider, Task<T>> action)
        {
            using var scope = _services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(tenantId, tenantSlug);
            return await action(scope.ServiceProvider);
        }

        public Task<Result<TenantStaffUserDto>> CreateUserAsync(
            Guid tenantId,
            string tenantSlug,
            string username,
            string password,
            TenantUserRole role) =>
            WithTenantAsync(tenantId, tenantSlug, async sp =>
                await sp.GetRequiredService<TenantStaffService>().CreateAsync(username, password, role));

        public Task<AdminLoginResult> SignInAsync(string tenantSlug, string username, string password) =>
            WithTenantlessScopeAsync(async sp =>
            {
                var context = new DefaultHttpContext { RequestServices = sp };
                context.Request.Scheme = "https";
                context.Request.Host = new HostString("admin.test");
                return await sp.GetRequiredService<AdminAuthService>()
                    .TrySignInAsync(context, tenantSlug, username, password);
            });

        public async Task<TenantAdminUser?> GetUserAsync(Guid tenantId, string username)
        {
            using var scope = _services.CreateScope();
            var normalized = TenantAdminUser.NormalizeUsername(username);
            return await scope.ServiceProvider.GetRequiredService<AppDbContext>().TenantAdminUsers
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(user => user.TenantId == tenantId && user.NormalizedUsername == normalized);
        }

        public async Task<TenantAdminUser?> GetUserByIdAsync(Guid userId)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AppDbContext>().TenantAdminUsers
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(user => user.Id == userId);
        }

        public async Task<bool> VerifyPasswordAsync(Guid userId, string password)
        {
            using var scope = _services.CreateScope();
            var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>().TenantAdminUsers
                .IgnoreQueryFilters()
                .SingleAsync(candidate => candidate.Id == userId);
            return scope.ServiceProvider.GetRequiredService<IPasswordHashingService>()
                .VerifyPassword(user.PasswordHash, password);
        }

        private async Task<T> WithTenantlessScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
        {
            using var scope = _services.CreateScope();
            return await action(scope.ServiceProvider);
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
                var subscription = await db.TenantSubscriptions.SingleAsync(s => s.TenantId == TenantSeed.KBeautyTenantId);
                db.Entry(subscription).Property(nameof(TenantSubscription.PaidThroughUtc)).CurrentValue = DateTime.UtcNow.AddDays(30);
                await db.SaveChangesAsync();
            }

            using (var scope = _services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Tenants.Add(new Tenant(BellaTenantId, BellaSlug, "Bella Staff", "America/Tijuana", DateTime.UtcNow));
                db.TenantBrandings.Add(new TenantBranding(BellaTenantId, primaryColor: "#8B5CF6", secondaryColor: "#F5D0FE"));
                db.TenantSubscriptions.Add(new TenantSubscription(
                    BellaTenantId,
                    TenantSubscriptionStatus.Active,
                    "test",
                    paidThroughUtc: DateTime.UtcNow.AddDays(30)));
                await db.SaveChangesAsync();
            }

            await SeedAdminAsync(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug, KBeautyAdminId, "owner");
            await SeedAdminAsync(BellaTenantId, BellaSlug, BellaAdminId, "owner");
        }

        private async Task SeedAdminAsync(Guid tenantId, string tenantSlug, Guid adminId, string username)
        {
            using var scope = _services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(tenantId, tenantSlug);
            var passwords = scope.ServiceProvider.GetRequiredService<IPasswordHashingService>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TenantAdminUsers.Add(new TenantAdminUser(
                adminId,
                tenantId,
                username,
                passwords.HashPassword(AdminPassword),
                DateTime.UtcNow,
                role: TenantUserRole.Admin));
            await db.SaveChangesAsync();
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
