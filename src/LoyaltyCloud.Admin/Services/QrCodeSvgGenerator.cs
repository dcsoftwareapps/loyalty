using QRCoder;

namespace LoyaltyCloud.Admin.Services;

public static class QrCodeSvgGenerator
{
    private const string DarkModuleColor = "#000000";
    private const string LightModuleColor = "#FFFFFF";

    public static string GenerateSvg(string value, int scale = 8, int quietZone = 4)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (scale <= 0)
            throw new ArgumentOutOfRangeException(nameof(scale), "Scale must be greater than zero.");
        if (quietZone < 4)
            throw new ArgumentOutOfRangeException(nameof(quietZone), "Quiet zone must be at least 4 modules.");

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(value, QRCodeGenerator.ECCLevel.Q);
        var qr = new SvgQRCode(data);

        return qr.GetGraphic(
            scale,
            DarkModuleColor,
            LightModuleColor,
            drawQuietZones: true,
            sizingMode: SvgQRCode.SizingMode.ViewBoxAttribute);
    }

    public static string GenerateDataUri(string value, int scale = 8, int quietZone = 4)
    {
        var svg = GenerateSvg(value, scale, quietZone);
        return "data:image/svg+xml;utf8," + Uri.EscapeDataString(svg);
    }
}
