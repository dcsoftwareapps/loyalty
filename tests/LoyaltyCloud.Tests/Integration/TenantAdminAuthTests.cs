extern alias AdminApp;

using System.Net;
using System.Security.Claims;
using AdminApp::LoyaltyCloud.Admin.Auth;
using LoyaltyCloud.Application;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class TenantAdminAuthTests
{
    private static readonly Guid BellaTenantId = Guid.Parse("b4000000-0000-0000-0000-000000000001");
    private static readonly Guid KBeautyAdminId = Guid.Parse("b4000000-0000-0000-0000-000000000101");
    private static readonly Guid BellaAdminId = Guid.Parse("b4000000-0000-0000-0000-000000000102");
    private const string BellaSlug = "bella-salon";
    private const string SharedUsername = "owner";
    private const string KBeautyPassword = "KBeautyAuth123!";
    private const string BellaPassword = "BellaAuth123!";

    [Fact]
    [Trait("Category", "TenantAdminAuth")]
    [Trait("Category", "NoDefaultTenant")]
    public async Task KBeauty_admin_can_login_on_kbeauty_slug()
    {
        await using var env = await AuthTestEnvironment.CreateAsync();

        var result = await env.SignInAsync("kbeauty", SharedUsername, KBeautyPassword);

        Assert.Equal(AdminLoginResult.Success, result.Result);
        Assert.Contains("loyaltycloud.admin.auth=", result.SetCookieHeader);
    }

    [Fact]
    [Trait("Category", "TenantAdminAuth")]
    [Trait("Category", "NoDefaultTenant")]
    public async Task Bella_admin_can_login_on_bella_slug()
    {
        await using var env = await AuthTestEnvironment.CreateAsync();

        var result = await env.SignInAsync(BellaSlug, SharedUsername, BellaPassword);

        Assert.Equal(AdminLoginResult.Success, result.Result);
        Assert.Contains("loyaltycloud.admin.auth=", result.SetCookieHeader);
    }

    [Fact]
    [Trait("Category", "TenantAdminAuth")]
    public async Task Same_username_can_exist_in_two_tenants()
    {
        await using var env = await AuthTestEnvironment.CreateAsync();

        var kbeauty = await env.SignInAsync("kbeauty", SharedUsername, KBeautyPassword);
        var bella = await env.SignInAsync(BellaSlug, SharedUsername, BellaPassword);

        Assert.Equal(AdminLoginResult.Success, kbeauty.Result);
        Assert.Equal(AdminLoginResult.Success, bella.Result);
    }

    [Fact]
    [Trait("Category", "TenantAdminAuth")]
    public async Task Wrong_password_fails()
    {
        await using var env = await AuthTestEnvironment.CreateAsync();

        var result = await env.SignInAsync("kbeauty", SharedUsername, "wrong-password");

        Assert.Equal(AdminLoginResult.InvalidCredentials, result.Result);
        Assert.Null(result.SetCookieHeader);
    }

    [Fact]
    [Trait("Category", "TenantAdminAuth")]
    public async Task Username_from_other_tenant_fails()
    {
        await using var env = await AuthTestEnvironment.CreateAsync();

        var result = await env.SignInAsync("kbeauty", "bella-only", BellaPassword);

        Assert.Equal(AdminLoginResult.InvalidCredentials, result.Result);
        Assert.Null(result.SetCookieHeader);
    }

    [Fact]
    [Trait("Category", "TenantAdminAuth")]
    public async Task Suspended_tenant_cannot_login()
    {
        await using var env = await AuthTestEnvironment.CreateAsync();
        await env.SetSubscriptionStatusAsync(BellaTenantId, TenantSubscriptionStatus.Suspended);

        var result = await env.SignInAsync(BellaSlug, SharedUsername, BellaPassword);

        Assert.Equal(AdminLoginResult.TenantUnavailable, result.Result);
    }

    [Fact]
    [Trait("Category", "TenantAdminAuth")]
    public async Task Inactive_admin_user_cannot_login()
    {
        await using var env = await AuthTestEnvironment.CreateAsync();
        await env.SetAdminActiveAsync(KBeautyAdminId, isActive: false);

        var result = await env.SignInAsync("kbeauty", SharedUsername, KBeautyPassword);

        Assert.Equal(AdminLoginResult.InvalidCredentials, result.Result);
    }

    [Fact]
    [Trait("Category", "TenantAdminAuth")]
    public void Principal_contains_tenant_id_claim()
    {
        var principal = AuthTestEnvironment.CreatePrincipal(TenantSeed.KBeautyTenantId, "kbeauty", KBeautyAdminId, SharedUsername);

        Assert.Equal(TenantSeed.KBeautyTenantId.ToString(), principal.FindFirstValue(AdminClaimTypes.TenantId));
        Assert.Equal("kbeauty", principal.FindFirstValue(AdminClaimTypes.TenantSlug));
    }

    [Fact]
    [Trait("Category", "TenantAdminAuth")]
    [Trait("Category", "NoDefaultTenant")]
    public async Task TenantContext_is_set_from_cookie_claims()
    {
        await using var env = await AuthTestEnvironment.CreateAsync();

        var tenant = await env.ResolveTenantFromPrincipalAsync(AuthTestEnvironment.CreatePrincipal(BellaTenantId, BellaSlug, BellaAdminId, SharedUsername));

        Assert.Equal(BellaTenantId, tenant.TenantId);
        Assert.Equal(BellaSlug, tenant.TenantSlug);
    }

    [Fact]
    [Trait("Category", "TenantAdminAuth")]
    public async Task KBeauty_user_cannot_query_bella_data()
    {
        await using var env = await AuthTestEnvironment.CreateAsync();

        var names = await env.QueryCustomersForPrincipalAsync(AuthTestEnvironment.CreatePrincipal(TenantSeed.KBeautyTenantId, "kbeauty", KBeautyAdminId, SharedUsername));

        Assert.Contains("KBeauty Auth Customer", names);
        Assert.DoesNotContain("Bella Auth Customer", names);
    }

    [Fact]
    [Trait("Category", "TenantAdminAuth")]
    public async Task Manipulated_tenant_slug_claim_is_rejected()
    {
        await using var env = await AuthTestEnvironment.CreateAsync();

        var resolved = await env.TryResolvePrincipalAsync(AuthTestEnvironment.CreatePrincipal(TenantSeed.KBeautyTenantId, BellaSlug, KBeautyAdminId, SharedUsername));

        Assert.False(resolved);
    }

    [Fact]
    [Trait("Category", "TenantAdminAuth")]
    [Trait("Category", "NoDefaultTenant")]
    public void Root_login_route_is_not_declared()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "LoyaltyCloud.Admin",
            "Pages",
            "Login.razor"));

        Assert.DoesNotContain("@page \"/login\"", source);
        Assert.Contains("@page \"/{TenantSlug}/login\"", source);
        Assert.DoesNotContain("Legacy" + "Default" + "Tenant" + "Slug", source);
        Assert.DoesNotContain("kbeauty/login", source);
    }

    [Fact]
    [Trait("Category", "TenantAdminAuth")]
    public async Task Logout_path_uses_tenant_slug_when_available()
    {
        await using var env = await AuthTestEnvironment.CreateAsync();

        var path = await env.GetLogoutRedirectPathAsync(AuthTestEnvironment.CreatePrincipal(BellaTenantId, BellaSlug, BellaAdminId, SharedUsername));

        Assert.Equal($"/{BellaSlug}/login", path);
    }

    private sealed class AuthTestEnvironment : IAsyncDisposable
    {
        private readonly ServiceProvider _services;
        private readonly string _connectionString;

        private AuthTestEnvironment(ServiceProvider services, string connectionString)
        {
            _services = services;
            _connectionString = connectionString;
        }

        public static async Task<AuthTestEnvironment> CreateAsync()
        {
            var dbName = "LoyaltyCloud_MT3B_" + Guid.NewGuid().ToString("N");
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
                    ["Wallet:UseRealApns"] = "false"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddApplication();
            services.AddInfrastructure(configuration, new TestHostEnvironment());
            services.Configure<AdminAuthOptions>(_ => { });
            services.AddScoped<AdminAuthService>();
            services
                .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options => options.Cookie.Name = "loyaltycloud.admin.auth");

            var provider = services.BuildServiceProvider(validateScopes: true);
            var env = new AuthTestEnvironment(provider, connectionString);
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

        public async Task<(AdminLoginResult Result, string? SetCookieHeader)> SignInAsync(
            string tenantSlug,
            string username,
            string password)
        {
            using var scope = _services.CreateScope();
            var httpContext = CreateHttpContext(scope.ServiceProvider);
            var result = await scope.ServiceProvider.GetRequiredService<AdminAuthService>()
                .TrySignInAsync(httpContext, tenantSlug, username, password);
            var setCookie = httpContext.Response.Headers.SetCookie.FirstOrDefault();
            return (result, setCookie);
        }

        public async Task SetSubscriptionStatusAsync(Guid tenantId, TenantSubscriptionStatus status)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var subscription = await db.TenantSubscriptions.SingleAsync(s => s.TenantId == tenantId);
            db.Entry(subscription).Property(nameof(TenantSubscription.Status)).CurrentValue = status;
            await db.SaveChangesAsync();
        }

        public async Task SetAdminActiveAsync(Guid adminUserId, bool isActive)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.TenantAdminUsers.IgnoreQueryFilters().SingleAsync(u => u.Id == adminUserId);
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(admin.TenantId, admin.TenantId == BellaTenantId ? BellaSlug : TenantSeed.KBeautySlug);
            db.Entry(admin).Property(nameof(TenantAdminUser.IsActive)).CurrentValue = isActive;
            await db.SaveChangesAsync();
        }

        public async Task<(Guid? TenantId, string? TenantSlug)> ResolveTenantFromPrincipalAsync(ClaimsPrincipal principal)
        {
            using var scope = _services.CreateScope();
            var context = CreateHttpContext(scope.ServiceProvider);
            context.User = principal;
            var ok = await scope.ServiceProvider.GetRequiredService<AdminAuthService>().TrySetTenantContextFromPrincipalAsync(context);
            Assert.True(ok);
            var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            return (tenantContext.TenantId, tenantContext.TenantSlug);
        }

        public async Task<bool> TryResolvePrincipalAsync(ClaimsPrincipal principal)
        {
            using var scope = _services.CreateScope();
            var context = CreateHttpContext(scope.ServiceProvider);
            context.User = principal;
            return await scope.ServiceProvider.GetRequiredService<AdminAuthService>().TrySetTenantContextFromPrincipalAsync(context);
        }

        public async Task<IReadOnlyList<string>> QueryCustomersForPrincipalAsync(ClaimsPrincipal principal)
        {
            using var scope = _services.CreateScope();
            var context = CreateHttpContext(scope.ServiceProvider);
            context.User = principal;
            var ok = await scope.ServiceProvider.GetRequiredService<AdminAuthService>().TrySetTenantContextFromPrincipalAsync(context);
            Assert.True(ok);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await db.Customers.OrderBy(c => c.FullName).Select(c => c.FullName).ToListAsync();
        }

        public async Task<string> GetLogoutRedirectPathAsync(ClaimsPrincipal principal)
        {
            using var scope = _services.CreateScope();
            var context = CreateHttpContext(scope.ServiceProvider);
            context.User = principal;
            await Task.CompletedTask;
            return scope.ServiceProvider.GetRequiredService<AdminAuthService>().GetLoginPathForCurrentPrincipal(context);
        }

        public static ClaimsPrincipal CreatePrincipal(Guid tenantId, string tenantSlug, Guid adminUserId, string username)
        {
            var claims = new[]
            {
                new Claim(AdminClaimTypes.Subject, adminUserId.ToString()),
                new Claim(AdminClaimTypes.TenantId, tenantId.ToString()),
                new Claim(AdminClaimTypes.TenantSlug, tenantSlug),
                new Claim(AdminClaimTypes.Name, username),
                new Claim(AdminClaimTypes.AuthTime, "0")
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme, AdminClaimTypes.Name, "role");
            return new ClaimsPrincipal(identity);
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
            await SeedAdminsAndCustomersAsync();
        }

        private async Task SeedBellaTenantAsync()
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Tenants.Add(new Tenant(BellaTenantId, BellaSlug, "Bella Salon", "America/Tijuana", DateTime.UtcNow));
            db.TenantBrandings.Add(new TenantBranding(BellaTenantId, primaryColor: "#8B5CF6", secondaryColor: "#F5D0FE"));
            db.TenantSubscriptions.Add(new TenantSubscription(BellaTenantId, TenantSubscriptionStatus.Active, "test"));
            await db.SaveChangesAsync();
        }

        private async Task SeedAdminsAndCustomersAsync()
        {
            var now = DateTime.UtcNow;

            using (var scope = _services.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug);
                var passwords = scope.ServiceProvider.GetRequiredService<IPasswordHashingService>();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.TenantAdminUsers.Add(new TenantAdminUser(KBeautyAdminId, TenantSeed.KBeautyTenantId, SharedUsername, passwords.HashPassword(KBeautyPassword), now));
                db.Customers.Add(new Customer(Guid.Parse("b4000000-0000-0000-0000-000000000201"), TenantSeed.KBeautyTenantId, "KBeauty Auth Customer", "auth.kbeauty@test.local", new DateTime(1990, 1, 1), now, "6461111111"));
                await db.SaveChangesAsync();
            }

            using (var scope = _services.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(BellaTenantId, BellaSlug);
                var passwords = scope.ServiceProvider.GetRequiredService<IPasswordHashingService>();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.TenantAdminUsers.Add(new TenantAdminUser(BellaAdminId, BellaTenantId, SharedUsername, passwords.HashPassword(BellaPassword), now));
                db.TenantAdminUsers.Add(new TenantAdminUser(Guid.Parse("b4000000-0000-0000-0000-000000000103"), BellaTenantId, "bella-only", passwords.HashPassword(BellaPassword), now));
                db.Customers.Add(new Customer(Guid.Parse("b4000000-0000-0000-0000-000000000202"), BellaTenantId, "Bella Auth Customer", "auth.bella@test.local", new DateTime(1990, 1, 1), now, "6462222222"));
                await db.SaveChangesAsync();
            }
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
}
