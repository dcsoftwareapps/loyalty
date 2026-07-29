using Microsoft.AspNetCore.Mvc;

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

    public WalletAssetsController(ILogger<WalletAssetsController> logger)
    {
        _logger = logger;
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
