using Microsoft.AspNetCore.Mvc;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Infrastructure.Services;
using LoyaltyCloud.Application.Stamps;
using LoyaltyCloud.Infrastructure.Services.GoogleWallet;

namespace LoyaltyCloud.API.Controllers;

[ApiController]
[Route("api/wallet-assets")]
public sealed class WalletAssetsController : ControllerBase
{
    private static readonly IReadOnlyDictionary<string, string> AppleWalletAssets =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["logo.png"] = Path.Combine("Assets", "AppleWallet", "logo.png"),
            ["logo@2x.png"] = Path.Combine("Assets", "AppleWallet", "logo@2x.png"),
            ["logo@3x.png"] = Path.Combine("Assets", "AppleWallet", "logo@3x.png")
        };

    private readonly ILogger<WalletAssetsController> _logger;
    private readonly ITenantWalletBrandingReadService _branding;
    private readonly ITenantWalletAssetProvider _assets;
    private readonly GoogleStampWalletAssetTokenService _stampTokens;
    private readonly IMutableTenantContext _tenantContext;
    private readonly IOperationalTenantReadService _operationalTenants;
    private readonly IStampWalletProjectionService _stampProjection;

    public WalletAssetsController(
        ILogger<WalletAssetsController> logger,
        ITenantWalletBrandingReadService branding,
        ITenantWalletAssetProvider assets,
        GoogleStampWalletAssetTokenService stampTokens,
        IMutableTenantContext tenantContext,
        IOperationalTenantReadService operationalTenants,
        IStampWalletProjectionService stampProjection)
    {
        _logger = logger;
        _branding = branding;
        _assets = assets;
        _stampTokens = stampTokens;
        _tenantContext = tenantContext;
        _operationalTenants = operationalTenants;
        _stampProjection = stampProjection;
    }

    [HttpGet("google/stamps/progress/{token}.png")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [Produces("image/png")]
    public async Task<IActionResult> GetGoogleStampProgress(string token, CancellationToken ct)
    {
        if (!_stampTokens.TryRead(token, out var payload)) return NotFound();
        var tenant = (await _operationalTenants.ListTenantsForExecutionAsync(ct))
            .SingleOrDefault(x => x.TenantId == payload.TenantId
                && string.Equals(x.Slug, payload.TenantSlug, StringComparison.OrdinalIgnoreCase));
        if (tenant is null || !tenant.IsOperational) return NotFound();
        if (_tenantContext.HasTenant && _tenantContext.TenantId != payload.TenantId) return NotFound();

        var ownsContext = !_tenantContext.HasTenant;
        if (ownsContext) _tenantContext.SetTenant(payload.TenantId, payload.TenantSlug);
        try
        {
            var projection = await _stampProjection.ReadAsync(payload.SerialNumber, ct);
            var branding = await _branding.GetCurrentAsync(ct);
            var rendered = _stampProjection.Render(projection, branding.BackgroundHex);
            var image = rendered.First(x => x.Name == "strip@3x.png");
            Response.Headers.CacheControl = "no-store";
            Response.Headers.XContentTypeOptions = "nosniff";
            return File(image.Bytes, "image/png");
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
        finally
        {
            if (ownsContext) _tenantContext.Clear();
        }
    }

    [HttpGet("google/{tenantId:guid}/logo.png")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [Produces("image/png")]
    public async Task<IActionResult> GetGoogleWalletLogo(Guid tenantId, CancellationToken ct)
    {
        TenantWalletBrandingDto branding;
        try
        {
            branding = await _branding.GetForTenantAsync(tenantId, ct);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }

        var logo = await _assets.LoadGoogleLogoAsync(
            branding.TenantId,
            branding.TenantSlug,
            branding.WalletLogoBlobName,
            branding.LogoBlobName,
            ct);

        Response.Headers.CacheControl = "no-store";
        return File(logo.Bytes, "image/png");
    }
    [HttpGet("google/{tenantId:guid}/hero.png")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [Produces("image/png")]
    public async Task<IActionResult> GetGoogleWalletHero(Guid tenantId, CancellationToken ct)
    {
        TenantWalletBrandingDto branding;
        try { branding = await _branding.GetForTenantAsync(tenantId, ct); }
        catch (InvalidOperationException) { return NotFound(); }
        if (string.IsNullOrWhiteSpace(branding.AppleWalletStripImageBlobName)) return NotFound();
        var assets = await _assets.LoadAssetsAsync(tenantId, branding.TenantSlug,
            branding.WalletLogoBlobName, branding.LogoBlobName, true,
            branding.AppleWalletStripImageBlobName, ct);
        var hero = assets.FirstOrDefault(x => x.Name == "strip@3x.png");
        return hero is null ? NotFound() : File(hero.Bytes, "image/png");
    }

    [HttpGet("apple/{assetName}")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    [Produces("image/png")]
    public IActionResult GetAppleWalletAsset(string assetName)
    {
        if (!AppleWalletAssets.TryGetValue(assetName, out var relativePath))
            return NotFound();

        var path = Path.Combine(AppContext.BaseDirectory, relativePath);
        if (!System.IO.File.Exists(path))
        {
            _logger.LogError("Bundled Apple Wallet asset was not found. Asset={AssetName}, Path={Path}", assetName, path);
            return NotFound();
        }

        Response.Headers.CacheControl = "public,max-age=3600";
        return PhysicalFile(path, "image/png");
    }
}
