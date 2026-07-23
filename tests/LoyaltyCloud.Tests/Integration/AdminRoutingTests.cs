extern alias AdminApp;

using System.Net;
using AdminApp::LoyaltyCloud.Admin.Auth;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using LoyaltyCloud.Infrastructure.Services;
using LoyaltyCloud.Tests.Integration.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class AdminRoutingTests : IClassFixture<AdminRoutingTests.AdminWebApplicationFactory>, IAsyncLifetime
{
    private const string SuperAdminUsername = "platform";
    private const string SuperAdminPassword = "Platform123!";
    private const string TenantAdminUsername = "owner";
    private const string TenantAdminPassword = "Tenant123!";

    private readonly AdminWebApplicationFactory _factory;

    public AdminRoutingTests(AdminWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync() => await _factory.EnsureDatabaseCreatedAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Trait("Category", "AdminRouting")]
    public async Task Root_redirects_to_platform_login_without_loop()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/platform/login", response.Headers.Location?.OriginalString);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public async Task Platform_login_is_anonymous()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/platform/login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public async Task Tenant_login_route_is_anonymous()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/kbeauty/login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public async Task Anonymous_platform_route_redirects_to_platform_login_without_double_platform()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/platform/tenants");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = Assert.IsType<Uri>(response.Headers.Location);
        Assert.Equal("/platform/login?returnUrl=%2Fplatform%2Ftenants", location.OriginalString);
        Assert.DoesNotContain("/platform/platform/login", location.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.Equals("/login", location.OriginalString, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public async Task Anonymous_slugless_admin_route_does_not_redirect_to_legacy_login()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/scan");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = Assert.IsType<Uri>(response.Headers.Location);
        Assert.Equal("/platform/login", location.AbsolutePath);
        Assert.Equal("?returnUrl=%2Fscan", location.Query);
        Assert.DoesNotContain("/login?ReturnUrl=", location.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public void Tenant_cookie_redirect_preserves_tenant_slug()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("admin.test");
        context.Request.Path = "/kbeauty/dashboard";

        var redirect = AdminLoginRedirects.BuildTenantAwareLoginRedirect(
            context.Request,
            "https://admin.test/login?ReturnUrl=%2Fkbeauty%2Fdashboard");

        Assert.Equal("/kbeauty/login?returnUrl=%2Fkbeauty%2Fdashboard", redirect);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public async Task Super_admin_authenticated_can_access_platform_tenants()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("Cookie", await _factory.CreateSuperAdminCookieAsync());

        using var response = await client.GetAsync("/platform/tenants");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public async Task Tenant_admin_authenticated_cannot_access_platform_tenants()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("Cookie", await _factory.CreateTenantAdminCookieAsync());

        using var response = await client.GetAsync("/platform/tenants");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = Assert.IsType<Uri>(response.Headers.Location);
        Assert.Equal("/platform/login?returnUrl=%2Fplatform%2Ftenants", location.OriginalString);
        Assert.DoesNotContain("/platform/platform/login", location.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public void Tenant_cookie_redirect_without_slug_uses_platform_login_not_legacy_login()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("admin.test");
        context.Request.Path = "/dashboard";

        var redirect = AdminLoginRedirects.BuildTenantAwareLoginRedirect(
            context.Request,
            "https://admin.test/login?ReturnUrl=%2Fdashboard");

        Assert.Equal("/platform/login", redirect);
        Assert.DoesNotContain("/login?ReturnUrl=", redirect, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    [Trait("Category", "AdminCustomerPoints")]
    public void Customer_detail_points_button_links_to_existing_scan_flow_with_serial_prefill()
    {
        var customerDetailSource = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "CustomerDetail.razor"));
        var scanSource = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "Scan.razor"));

        Assert.Contains("href=\"@ScanHref()\"", customerDetailSource);
        Assert.Contains("/scan?serial=", customerDetailSource);
        Assert.Contains("Uri.EscapeDataString(detail.Wallet.SerialNumber)", customerDetailSource);
        Assert.DoesNotContain("Nav.NavigateTo($\"/scan?serial=", customerDetailSource, StringComparison.Ordinal);
        Assert.Contains("[SupplyParameterFromQuery] public string? Serial", scanSource);
        Assert.Contains("await SearchAsync();", scanSource);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    [Trait("Category", "AdminCustomerPoints")]
    public void Direct_scan_route_remains_available_for_general_add_points_flow()
    {
        var scanSource = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "Scan.razor"));

        Assert.Contains("@page \"/scan\"", scanSource);
        Assert.Contains("Escanear QR", scanSource);
        Assert.Contains("Serial de la clienta", scanSource);
    }

    [Fact]
    [Trait("Category", "AdminRouting")]
    public void Admin_cookie_options_do_not_use_legacy_root_login_path()
    {
        var source = File.ReadAllText(Path.Combine(GetRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Program.cs"));

        Assert.DoesNotContain("LoginPath = \"/login\"", source);
        Assert.DoesNotContain("AccessDeniedPath = \"/login\"", source);
    }

    private static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "src")))
            current = current.Parent;

        return current?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    public sealed class AdminWebApplicationFactory : WebApplicationFactory<AdminApp::Program>
    {
        private readonly string _dbName = "LoyaltyCloudAdminRouting-" + Guid.NewGuid().ToString("N");
        private readonly FakeApnService _apn = new();
        private readonly FakeStorageService _storage = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Server=(test);Database=Test;",
                    ["Admin:ApiBaseUrl"] = "https://api.test/",
                    ["Azure:KeyVaultUri"] = "",
                    ["Azure:BlobStorage:ConnectionString"] = "",
                    ["Apple:PassTypeIdentifier"] = "pass.com.kbeautymx.loyalty",
                    ["Apple:TeamIdentifier"] = "TESTTEAM01",
                    ["Apple:WebServiceURL"] = "https://api.test",
                    ["Apple:OrganizationName"] = "LoyaltyCloud Test",
                    ["Wallet:UseRealPassSigning"] = "false",
                    ["Wallet:UseRealApns"] = "false",
                    ["SuperAdmin:Username"] = SuperAdminUsername,
                    ["SuperAdmin:PasswordHash"] = new PasswordHashingService().HashPassword(SuperAdminPassword),
                    ["SuperAdmin:SessionHours"] = "8"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<AppDbContext>>();
                services.AddDbContext<AppDbContext>(opts => opts.UseInMemoryDatabase(_dbName));

                services.RemoveAll<IPassGeneratorService>();
                services.RemoveAll<IApnService>();
                services.RemoveAll<IStorageService>();

                services.AddSingleton<IPassGeneratorService, FakePassGeneratorService>();
                services.AddSingleton<IApnService>(_apn);
                services.AddSingleton<IStorageService>(_storage);
            });
        }

        public async Task EnsureDatabaseCreatedAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();

            var subscription = await db.TenantSubscriptions.SingleAsync(s => s.TenantId == TenantSeed.KBeautyTenantId);
            db.Entry(subscription).Property(nameof(TenantSubscription.PaidThroughUtc)).CurrentValue = DateTime.UtcNow.AddDays(30);

            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug);
            if (!await db.TenantAdminUsers.AnyAsync(u => u.TenantId == TenantSeed.KBeautyTenantId && u.NormalizedUsername == TenantAdminUser.NormalizeUsername(TenantAdminUsername)))
            {
                var passwords = scope.ServiceProvider.GetRequiredService<IPasswordHashingService>();
                db.TenantAdminUsers.Add(new TenantAdminUser(
                    Guid.Parse("b4000000-0000-0000-0000-000000009001"),
                    TenantSeed.KBeautyTenantId,
                    TenantAdminUsername,
                    passwords.HashPassword(TenantAdminPassword),
                    DateTime.UtcNow));
            }

            await db.SaveChangesAsync();
        }

        public async Task<string> CreateSuperAdminCookieAsync()
        {
            using var scope = Services.CreateScope();
            var context = CreateHttpContext(scope.ServiceProvider);
            var result = await scope.ServiceProvider.GetRequiredService<SuperAdminAuthService>()
                .TrySignInAsync(context, SuperAdminUsername, SuperAdminPassword);

            Assert.Equal(SuperAdminLoginResult.Success, result);
            return ExtractCookie(context, "loyaltycloud.platform.auth");
        }

        public async Task<string> CreateTenantAdminCookieAsync()
        {
            using var scope = Services.CreateScope();
            var context = CreateHttpContext(scope.ServiceProvider);
            var result = await scope.ServiceProvider.GetRequiredService<AdminAuthService>()
                .TrySignInAsync(context, TenantSeed.KBeautySlug, TenantAdminUsername, TenantAdminPassword);

            Assert.Equal(AdminLoginResult.Success, result);
            return ExtractCookie(context, "loyaltycloud.admin.auth");
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

        private static string ExtractCookie(DefaultHttpContext context, string cookieName)
        {
            var setCookie = context.Response.Headers.SetCookie
                .FirstOrDefault(value => value?.StartsWith(cookieName + "=", StringComparison.OrdinalIgnoreCase) == true);

            Assert.False(string.IsNullOrWhiteSpace(setCookie));
            return setCookie!.Split(';', 2)[0];
        }
    }
}
