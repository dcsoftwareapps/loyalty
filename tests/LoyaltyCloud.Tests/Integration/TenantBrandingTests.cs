using LoyaltyCloud.Application;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class TenantBrandingTests
{
    private static readonly Guid BellaTenantId = Guid.Parse("b5000000-0000-0000-0000-000000000001");
    private static readonly Guid BrokenTenantId = Guid.Parse("b5000000-0000-0000-0000-000000000002");
    private const string BellaSlug = "bella-salon";
    private const string BrokenSlug = "broken-brand";

    [Fact]
    [Trait("Category", "TenantBranding")]
    public async Task KBeauty_and_Bella_return_their_own_branding()
    {
        await using var env = await BrandingTestEnvironment.CreateAsync();

        var kbeauty = await env.ResolvePublicAsync(TenantSeed.KBeautySlug);
        var bella = await env.ResolvePublicAsync(BellaSlug);

        Assert.NotNull(kbeauty);
        Assert.NotNull(bella);
        Assert.Equal("KBeauty", kbeauty!.DisplayName);
        Assert.Equal("#1C1C1C", kbeauty.PrimaryColor);
        Assert.Equal("Bella Salon", bella!.DisplayName);
        Assert.Equal("#8B5CF6", bella.PrimaryColor);
        Assert.Equal("#F5D0FE", bella.SecondaryColor);
    }

    [Fact]
    [Trait("Category", "TenantBranding")]
    public async Task Invalid_color_and_url_values_use_safe_fallbacks()
    {
        await using var env = await BrandingTestEnvironment.CreateAsync();

        var broken = await env.ResolvePublicAsync(BrokenSlug);

        Assert.NotNull(broken);
        Assert.Equal("#111827", broken!.PrimaryColor);
        Assert.Equal("#F3F4F6", broken.SecondaryColor);
        Assert.Null(broken.LogoUrl);
        Assert.Null(broken.InstagramUrl);
    }

    [Fact]
    [Trait("Category", "TenantBranding")]
    public async Task Admin_branding_is_loaded_from_current_tenant_context()
    {
        await using var env = await BrandingTestEnvironment.CreateAsync();

        var branding = await env.ReadBrandingAsync(BellaTenantId, BellaSlug);

        Assert.Equal(BellaTenantId, branding.TenantId);
        Assert.Equal(BellaSlug, branding.TenantSlug);
        Assert.Equal("Bella Salon", branding.DisplayName);
        Assert.Equal("#8B5CF6", branding.PrimaryColor);
    }

    [Fact]
    [Trait("Category", "TenantBranding")]
    public async Task Wallet_branding_uses_tenant_display_name_and_colors()
    {
        await using var env = await BrandingTestEnvironment.CreateAsync();

        var wallet = await env.ReadWalletBrandingAsync(BellaTenantId, BellaSlug);

        Assert.Equal(BellaSlug, wallet.TenantSlug);
        Assert.Equal("Bella Salon", wallet.OrganizationName);
        Assert.Equal("Tarjeta de Lealtad Bella Salon", wallet.Description);
        Assert.Equal("rgb(139,92,246)", wallet.ForegroundColor);
        Assert.Equal("rgb(245,208,254)", wallet.LabelColor);
        Assert.Contains("instagram.com/bella_salon", wallet.ContactValue);
    }

    [Fact]
    [Trait("Category", "TenantBranding")]
    public async Task Unknown_tenant_does_not_return_another_tenant_branding()
    {
        await using var env = await BrandingTestEnvironment.CreateAsync();

        var tenant = await env.ResolvePublicAsync("missing-brand");

        Assert.Null(tenant);
    }

    [Fact]
    [Trait("Category", "TenantBranding")]
    public void Join_login_and_admin_shell_are_tenant_branding_aware()
    {
        var root = GetRepositoryRoot();
        var join = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Admin", "Pages", "Join.razor"));
        var login = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Admin", "Pages", "Login.razor"));
        var layout = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Admin", "Components", "Layout", "MainLayout.razor"));

        Assert.Contains("@page \"/{TenantSlug}/join\"", join);
        Assert.Contains("tenant.LogoUrl", join);
        Assert.Contains("tenant.PrimaryColor", join);
        Assert.Contains("@page \"/{TenantSlug}/login\"", login);
        Assert.Contains("TenantResolver.ResolveBySlugAsync", login);
        Assert.Contains("tenant.LogoUrl", login);
        Assert.Contains("ITenantBrandingReadService", layout);
        Assert.Contains("branding.DisplayName", layout);
    }

    [Fact]
    [Trait("Category", "TenantBranding")]
    public void Wallet_asset_provider_uses_tenant_path_generic_fallback_and_kbeauty_legacy_fallback()
    {
        var root = GetRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Infrastructure", "Services", "TenantWalletAssetProvider.cs"));

        Assert.Contains("tenants/{tenantSlug}/branding/wallet/{spec.Name}", source);
        Assert.Contains("AppleWalletGeneric", source);
        Assert.Contains("TenantSeed.KBeautySlug", source);
        Assert.Contains("\"legacy-kbeauty\"", source);
    }

    [Fact]
    [Trait("Category", "TenantBranding")]
    public void Generic_wallet_assets_exist_with_required_names()
    {
        var root = GetRepositoryRoot();
        var dir = Path.Combine(root, "src", "LoyaltyCloud.Infrastructure", "Assets", "AppleWalletGeneric");
        var required = new[]
        {
            "icon.png",
            "icon@2x.png",
            "icon@3x.png",
            "logo.png",
            "logo@2x.png",
            "logo@3x.png"
        };

        foreach (var name in required)
            Assert.True(File.Exists(Path.Combine(dir, name)), $"Missing generic wallet asset: {name}");
    }

    private sealed class BrandingTestEnvironment : IAsyncDisposable
    {
        private readonly ServiceProvider _services;

        private BrandingTestEnvironment(ServiceProvider services)
        {
            _services = services;
        }

        public static async Task<BrandingTestEnvironment> CreateAsync()
        {
            var dbName = "LoyaltyCloud_MT3D_" + Guid.NewGuid().ToString("N");
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
                    ["Wallet:UseRealApns"] = "false"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddApplication();
            services.AddInfrastructure(configuration, new TestHostEnvironment());

            var provider = services.BuildServiceProvider(validateScopes: true);
            var env = new BrandingTestEnvironment(provider);
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

        public async Task<PublicTenantInfo?> ResolvePublicAsync(string tenantSlug)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IPublicTenantResolver>().ResolveBySlugAsync(tenantSlug);
        }

        public async Task<TenantBrandingInfo> ReadBrandingAsync(Guid tenantId, string tenantSlug)
        {
            using var scope = _services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(tenantId, tenantSlug);
            return await scope.ServiceProvider.GetRequiredService<ITenantBrandingReadService>().GetCurrentAsync();
        }

        public async Task<TenantWalletBrandingDto> ReadWalletBrandingAsync(Guid tenantId, string tenantSlug)
        {
            using var scope = _services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(tenantId, tenantSlug);
            return await scope.ServiceProvider.GetRequiredService<ITenantWalletBrandingReadService>().GetCurrentAsync();
        }

        private async Task InitializeAsync()
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureDeletedAsync();
            await db.Database.MigrateAsync();

            db.Tenants.Add(new Tenant(BellaTenantId, BellaSlug, "Bella Salon", "America/Tijuana", DateTime.UtcNow));
            db.TenantBrandings.Add(new TenantBranding(
                BellaTenantId,
                primaryColor: "#8B5CF6",
                secondaryColor: "#F5D0FE",
                supportPhone: "+52 646 000 0000",
                whatsAppUrl: "https://wa.me/526460000000",
                instagramUrl: "https://instagram.com/bella_salon",
                termsUrl: "https://bella-salon.example/terminos"));
            db.TenantSubscriptions.Add(new TenantSubscription(BellaTenantId, TenantSubscriptionStatus.Active, "test"));

            db.Tenants.Add(new Tenant(BrokenTenantId, BrokenSlug, "Broken Brand", "America/Tijuana", DateTime.UtcNow));
            db.TenantBrandings.Add(new TenantBranding(
                BrokenTenantId,
                logoUrl: "javascript:alert(1)",
                primaryColor: "not-a-color",
                secondaryColor: "#XYZ",
                instagramUrl: "data:text/html,broken"));
            db.TenantSubscriptions.Add(new TenantSubscription(BrokenTenantId, TenantSubscriptionStatus.Active, "test"));

            await db.SaveChangesAsync();
        }
    }

    private static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "src")))
            current = current.Parent;

        return current?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
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
