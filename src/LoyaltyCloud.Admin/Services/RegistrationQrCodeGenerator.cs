using LoyaltyCloud.Application.Common.Interfaces;
using QRCoder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LoyaltyCloud.Admin.Services;

/// <summary>Public registration only. Does not change Wallet, operational or Gift Card QR codes.</summary>
public sealed class RegistrationQrCodeGenerator(
    ITenantContext tenantContext,
    ITenantBrandingLogoService logos,
    ILogger<RegistrationQrCodeGenerator> logger)
{
    public async Task<string> GenerateDataUriAsync(string registrationUrl, CancellationToken ct = default)
    {
        byte[]? logo = null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try { logo = await logos.ReadRegistrationLogoAsync(tenantContext.RequireTenantId(), timeout.Token); }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning("Could not load the current registration QR logo; using a plain QR.");
        }
        return "data:image/png;base64," + Convert.ToBase64String(GeneratePng(registrationUrl, logo));
    }

    public static byte[] GeneratePng(string value, byte[]? logoBytes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(value, QRCodeGenerator.ECCLevel.H);
        const int scale = 16;
        const int quietZone = 4; // QRCoder includes four modules on each edge.
        var count = data.ModuleMatrix.Count;
        var size = count * scale;
        using var image = new Image<Rgba32>(size, size, new Rgba32(255, 255, 255));
        void PaintModule(int x, int y)
        {
            var color = data.ModuleMatrix[y][x] ? Color.Black.ToPixel<Rgba32>() : Color.White.ToPixel<Rgba32>();
            for (var dy = 0; dy < scale; dy++)
                for (var dx = 0; dx < scale; dx++) image[x * scale + dx, y * scale + dy] = color;
        }
        for (var y = 0; y < count; y++)
            for (var x = 0; x < count; x++) PaintModule(x, y);

        if (logoBytes is { Length: > 0 and <= 2097152 })
        {
            try
            {
                var info = Image.Identify(logoBytes);
                if (info is not null && (long)info.Width * info.Height <= 16_000_000)
                {
                    using var logo = Image.Load<Rgba32>(new DecoderOptions { MaxFrames = 1, SkipMetadata = true }, logoBytes);
                    // White backing + image occupy 18% of the full QR width, including quiet zone.
                    var box = (int)(size * .18);
                    var padding = Math.Max(4, box / 12);
                    var content = box - padding * 2;
                    logo.Mutate(x => x.Resize(new ResizeOptions { Size = new Size(content, content), Mode = ResizeMode.Max }));
                    using var patch = new Image<Rgba32>(box, box, new Rgba32(255, 255, 255));
                    patch.Mutate(x => x.DrawImage(logo, new Point((box - logo.Width) / 2, (box - logo.Height) / 2), 1));
                    image.Mutate(x => x.DrawImage(patch, new Point((size - box) / 2, (size - box) / 2), 1));

                    // Larger QR versions can have an alignment pattern at the center.
                    // Restore every alignment pattern after compositing, so none are covered.
                    var modules = count - quietZone * 2;
                    var version = (modules - 17) / 4;
                    if (version >= 2)
                    {
                        var alignCount = version / 7 + 2;
                        var step = version == 32 ? 26 : (version * 4 + alignCount * 2 + 1) / (alignCount * 2 - 2) * 2;
                        var centers = new int[alignCount];
                        centers[0] = 6;
                        for (var i = alignCount - 1; i > 0; i--) centers[i] = modules - 7 - (alignCount - 1 - i) * step;
                        for (var row = 0; row < alignCount; row++)
                            for (var col = 0; col < alignCount; col++)
                            {
                                if ((row == 0 && (col == 0 || col == alignCount - 1)) || (col == 0 && row == alignCount - 1)) continue;
                                for (var dy = -2; dy <= 2; dy++)
                                    for (var dx = -2; dx <= 2; dx++) PaintModule(centers[col] + quietZone + dx, centers[row] + quietZone + dy);
                            }
                    }
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Processing failures must also leave a complete traditional QR, never a partial overlay.
                for (var y = 0; y < count; y++)
                    for (var x = 0; x < count; x++) PaintModule(x, y);
            }
        }
        using var output = new MemoryStream();
        image.SaveAsPng(output);
        return output.ToArray();
    }
}
