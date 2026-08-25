using LoyaltyCloud.Application;
using LoyaltyCloud.Application.Common.Branding;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure;
using LoyaltyCloud.Infrastructure.Persistence;
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
    public async Task Bella_returns_its_own_branding()
    {
        await using var env = await BrandingTestEnvironment.CreateAsync();

        var bella = await env.ResolvePublicAsync(BellaSlug);

        Assert.NotNull(bella);
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
        Assert.Equal("rgb(139,92,246)", wallet.BackgroundColor);
        Assert.Equal("rgb(17,24,39)", wallet.ForegroundColor);
        Assert.Equal("rgb(17,24,39)", wallet.LabelColor);
        Assert.Contains("instagram.com/bella_salon", wallet.ContactValue);
    }

    [Fact]
    [Trait("Category", "TenantBranding")]
    public async Task Wallet_branding_uses_wallet_background_color_with_automatic_light_contrast()
    {
        await using var env = await BrandingTestEnvironment.CreateAsync();
        await env.SetWalletBackgroundColorAsync(BellaTenantId, "#1c1c1c");

        var wallet = await env.ReadWalletBrandingAsync(BellaTenantId, BellaSlug);

        Assert.Equal("#1C1C1C", wallet.BackgroundHex);
        Assert.Equal("rgb(28,28,28)", wallet.BackgroundColor);
        Assert.Equal("rgb(255,255,255)", wallet.ForegroundColor);
        Assert.Equal("rgb(255,255,255)", wallet.LabelColor);
    }

    [Fact]
    [Trait("Category", "TenantBranding")]
    public async Task Wallet_branding_without_wallet_background_falls_back_to_primary_color()
    {
        await using var env = await BrandingTestEnvironment.CreateAsync();

        var wallet = await env.ReadWalletBrandingAsync(BellaTenantId, BellaSlug);

        Assert.Equal("#8B5CF6", wallet.BackgroundHex);
        Assert.Equal("rgb(139,92,246)", wallet.BackgroundColor);
        Assert.Equal("rgb(17,24,39)", wallet.ForegroundColor);
        Assert.Equal("rgb(17,24,39)", wallet.LabelColor);
    }

    [Fact]
    [Trait("Category", "TenantBranding")]
    public void Wallet_color_contrast_uses_dark_text_on_light_background()
    {
        var colors = WalletColorContrast.ResolveTextColors("#FFFFFF");

        Assert.Equal("#111827", colors.ForegroundHex);
        Assert.Equal("#111827", colors.LabelHex);
    }

    [Theory]
    [Trait("Category", "TenantBranding")]
    [InlineData("#123456", true)]
    [InlineData("#ABCDEF", true)]
    [InlineData("#FFF", false)]
    [InlineData("123456", false)]
    [InlineData("#XYZXYZ", false)]
    public void Wallet_background_color_requires_rrggbb_hex(string value, bool expected)
    {
        Assert.Equal(expected, WalletColorContrast.IsHexColor(value));
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
    public void Wallet_asset_provider_uses_tenant_id_path_and_generic_fallback()
    {
        var root = GetRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Infrastructure", "Services", "TenantWalletAssetProvider.cs"));

        Assert.Contains("GetTenantBrandingPrefix(tenantId)", source);
        Assert.Contains("\"wallet-branding\"", source);
        Assert.Contains("tenant-branding/{tenantId:D}", File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Infrastructure", "Services", "TenantBrandingLogoService.cs")));
        Assert.Contains("AppleWalletGeneric", source);
        Assert.DoesNotContain("TenantSeed.KBeautySlug", source);
        Assert.DoesNotContain("legacy-kbeauty", source);
    }

    [Fact]
    [Trait("Category", "TenantBranding")]
    public void Config_wallet_preview_matches_real_pass_structure_and_uses_api_for_mutations()
    {
        var root = GetRepositoryRoot();
        var page = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Admin", "Pages", "Config.razor"));
        var css = File.ReadAllText(Path.Combine(root, "src", "LoyaltyCloud.Admin", "wwwroot", "css", "site.css"));

        Assert.Contains("Vista previa de Apple Wallet", page);
        Assert.Contains("Vista previa aproximada.", page);
        Assert.Contains("PUNTOS", page);
        Assert.Contains("50 pts", page);
        Assert.Contains("NIVEL", page);
        Assert.Contains("Mist ✨", page);
        Assert.Contains("PRÓXIMO", page);
        Assert.Contains("Glow", page);
        Assert.Contains("FALTAN", page);
        Assert.Contains("950 pts", page);
        Assert.Contains("kb-wallet-preview-field-labels", page);
        Assert.Contains("kb-wallet-preview-field-values", page);
        Assert.Contains("kb-wallet-preview-qr-pattern", page);
        Assert.DoesNotContain("Presenta este código en caja", page);
        Assert.DoesNotContain("La apariencia final puede variar ligeramente en Apple Wallet.", page);
        Assert.Contains("api/config/wallet-branding", page);
        Assert.Contains("api/config/wallet-branding/logo", page);
        Assert.DoesNotContain("new UpdateWalletCardBrandingCommand", page);
        Assert.DoesNotContain("new UploadTenantWalletLogoCommand", page);
        Assert.DoesNotContain("new RemoveTenantWalletLogoCommand", page);
        Assert.Contains("object-fit: contain", css);
        Assert.Contains(".kb-wallet-preview-qr", css);
        Assert.Contains(".kb-wallet-preview-field-labels", css);
        Assert.Contains(".kb-wallet-preview-field-values", css);
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

    [Fact]
    [Trait("Category", "TenantBranding")]
    public async Task Logo_upload_rejects_invalid_format_before_blob_storage()
    {
        await using var env = await BrandingTestEnvironment.CreateAsync(blobConnectionString: "");

        var result = await env.UploadLogoAsync(
            BellaTenantId,
            "logo.gif",
            "image/gif",
            new byte[] { 1, 2, 3 });

        Assert.True(result.IsFailure);
        Assert.Contains("El logo debe ser PNG o JPG.", result.Errors);
    }

    [Fact]
    [Trait("Category", "TenantBranding")]
    public async Task Logo_upload_rejects_invalid_size_before_blob_storage()
    {
        await using var env = await BrandingTestEnvironment.CreateAsync(blobConnectionString: "");

        var result = await env.UploadLogoAsync(
            BellaTenantId,
            "logo.png",
            "image/png",
            Array.Empty<byte>());

        Assert.True(result.IsFailure);
        Assert.Contains("El logo debe pesar máximo 2 MB.", result.Errors);
    }

    private sealed class BrandingTestEnvironment : IAsyncDisposable
    {
        private readonly ServiceProvider _services;

        private BrandingTestEnvironment(ServiceProvider services)
        {
            _services = services;
        }

        public static async Task<BrandingTestEnvironment> CreateAsync(string blobConnectionString = "UseDevelopmentStorage=true")
        {
            var dbName = "LoyaltyCloud_MT3D_" + Guid.NewGuid().ToString("N");
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True;",
                    ["Azure:KeyVaultUri"] = "",
                    ["Azure:BlobStorage:ConnectionString"] = blobConnectionString,
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

        public async Task<LoyaltyCloud.Common.Results.Result<LoyaltyCloud.Application.Common.Interfaces.TenantBrandingLogoResult>> UploadLogoAsync(
            Guid tenantId,
            string fileName,
            string contentType,
            byte[] bytes)
        {
            using var scope = _services.CreateScope();
            await using var stream = new MemoryStream(bytes);
            return await scope.ServiceProvider.GetRequiredService<ITenantBrandingLogoService>()
                .UploadAsync(tenantId, fileName, contentType, stream, bytes.LongLength);
        }

        public async Task SetWalletBackgroundColorAsync(Guid tenantId, string? color)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var branding = await db.TenantBrandings.SingleAsync(x => x.TenantId == tenantId);
            branding.SetWalletBackgroundColor(color);
            await db.SaveChangesAsync();
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
