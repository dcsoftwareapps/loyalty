extern alias AdminApp;

using System.Globalization;
using System.Text.RegularExpressions;
using AdminApp::LoyaltyCloud.Admin.Services;
using ZXing;
using ZXing.Common;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed partial class QuickHelpQrCodeTests
{
    public static TheoryData<string> RegistrationUrls => new()
    {
        "https://admin.loyaltycloud.net/a/join",
        "https://admin.loyaltycloud.net/kbeauty/join",
        "https://admin.loyaltycloud.net/bitcafe/join",
        "https://admin.loyaltycloud.net/salon-bella-del-mar-2026/join",
        "https://admin.loyaltycloud.net/cafe-mx-123/join"
    };

    [Theory]
    [MemberData(nameof(RegistrationUrls))]
    [Trait("Category", "QuickHelpQr")]
    public void Registration_qr_svg_round_trips_through_real_decoder(string url)
    {
        var svg = QrCodeSvgGenerator.GenerateSvg(url, scale: 7);

        var decoded = DecodeSvgQr(svg);

        Assert.Equal(url, decoded);
    }

    [Fact]
    [Trait("Category", "QuickHelpQr")]
    public void Registration_qr_uses_plain_black_white_svg_with_quiet_zone()
    {
        var svg = QrCodeSvgGenerator.GenerateSvg("https://admin.loyaltycloud.net/kbeauty/join", scale: 7);

        Assert.Contains("#000000", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#FFFFFF", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gradient", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("image", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Matches("viewBox=\"0 0 \\d+ \\d+\"", svg);
    }

    [Fact]
    [Trait("Category", "QuickHelpQr")]
    public void Quick_help_builds_tenant_join_url_from_configured_public_admin_base_url()
    {
        var page = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "LoyaltyCloud.Admin", "Pages", "QuickHelp.razor"));

        Assert.Contains("Configuration[\"Admin:PublicBaseUrl\"]", page, StringComparison.Ordinal);
        Assert.Contains("GetPublicAdminBaseUri()", page, StringComparison.Ordinal);
        Assert.Contains("return new Uri(Navigation.BaseUri);", page, StringComparison.Ordinal);
        Assert.Contains("$\"{tenantSlug.Trim().ToLowerInvariant()}/join\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("loyaltycloud-admin-894839", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://admin.loyaltycloud.net", page, StringComparison.OrdinalIgnoreCase);
    }

    private static string? DecodeSvgQr(string svg)
    {
        var source = SvgQrLuminanceSource.FromSvg(svg, pixelsPerModule: 8);
        var bitmap = new BinaryBitmap(new HybridBinarizer(source));
        var reader = new MultiFormatReader();
        var result = reader.decode(bitmap);
        return result?.Text;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LoyaltyCloud.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be found.");
    }

    private sealed partial class SvgQrLuminanceSource : LuminanceSource
    {
        private readonly byte[] luminances;

        private SvgQrLuminanceSource(byte[] luminances, int width, int height)
            : base(width, height)
        {
            this.luminances = luminances;
        }

        public override byte[] Matrix => luminances;

        public static SvgQrLuminanceSource FromSvg(string svg, int pixelsPerModule)
        {
            var viewBox = ViewBoxRegex().Match(svg);
            if (!viewBox.Success)
                throw new InvalidOperationException("QR SVG does not include a numeric viewBox.");

            var moduleWidth = (int)Math.Ceiling(ParseDecimal(viewBox.Groups["width"].Value));
            var moduleHeight = (int)Math.Ceiling(ParseDecimal(viewBox.Groups["height"].Value));
            var width = moduleWidth * pixelsPerModule;
            var height = moduleHeight * pixelsPerModule;
            var pixels = Enumerable.Repeat((byte)255, width * height).ToArray();

            foreach (Match rect in RectRegex().Matches(svg))
            {
                var attrs = ParseAttributes(rect.Groups["attrs"].Value);
                if (!IsDarkFill(attrs))
                    continue;

                Paint(
                    pixels,
                    width,
                    pixelsPerModule,
                    ParseDecimal(attrs.GetValueOrDefault("x", "0")),
                    ParseDecimal(attrs.GetValueOrDefault("y", "0")),
                    ParseDecimal(attrs.GetValueOrDefault("width", "0")),
                    ParseDecimal(attrs.GetValueOrDefault("height", "0")));
            }

            foreach (Match path in PathRegex().Matches(svg))
            {
                var attrs = ParseAttributes(path.Groups["attrs"].Value);
                if (!IsDarkFill(attrs))
                    continue;

                foreach (Match command in PathRectRegex().Matches(attrs.GetValueOrDefault("d", string.Empty)))
                {
                    Paint(
                        pixels,
                        width,
                        pixelsPerModule,
                        ParseDecimal(command.Groups["x"].Value),
                        ParseDecimal(command.Groups["y"].Value),
                        ParseDecimal(command.Groups["width"].Value),
                        ParseDecimal(command.Groups["height"].Value));
                }
            }

            return new SvgQrLuminanceSource(pixels, width, height);
        }

        public override byte[] getRow(int y, byte[]? row)
        {
            row ??= new byte[Width];
            Array.Copy(luminances, y * Width, row, 0, Width);
            return row;
        }

        private static void Paint(
            byte[] pixels,
            int imageWidth,
            int pixelsPerModule,
            decimal x,
            decimal y,
            decimal width,
            decimal height)
        {
            var startX = (int)Math.Round(x * pixelsPerModule);
            var startY = (int)Math.Round(y * pixelsPerModule);
            var endX = (int)Math.Round((x + width) * pixelsPerModule);
            var endY = (int)Math.Round((y + height) * pixelsPerModule);

            for (var yy = startY; yy < endY; yy++)
            {
                for (var xx = startX; xx < endX; xx++)
                    pixels[yy * imageWidth + xx] = 0;
            }
        }

        private static Dictionary<string, string> ParseAttributes(string value) =>
            AttributeRegex()
                .Matches(value)
                .ToDictionary(
                    match => match.Groups["name"].Value,
                    match => match.Groups["value"].Value,
                    StringComparer.OrdinalIgnoreCase);

        private static bool IsDarkFill(IReadOnlyDictionary<string, string> attrs) =>
            attrs.TryGetValue("fill", out var fill)
            && string.Equals(fill, "#000000", StringComparison.OrdinalIgnoreCase);

        private static decimal ParseDecimal(string value) =>
            decimal.Parse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

        [GeneratedRegex("viewBox=\"0\\s+0\\s+(?<width>[\\d.]+)\\s+(?<height>[\\d.]+)\"", RegexOptions.IgnoreCase)]
        private static partial Regex ViewBoxRegex();

        [GeneratedRegex("""<rect(?<attrs>[^>]*)/?>""", RegexOptions.IgnoreCase)]
        private static partial Regex RectRegex();

        [GeneratedRegex("""<path(?<attrs>[^>]*)/?>""", RegexOptions.IgnoreCase)]
        private static partial Regex PathRegex();

        [GeneratedRegex("(?<name>[\\w:-]+)=\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase)]
        private static partial Regex AttributeRegex();

        [GeneratedRegex("""M\s*(?<x>[\d.]+)\s*,?\s*(?<y>[\d.]+)\s*h\s*(?<width>[\d.]+)\s*v\s*(?<height>[\d.]+)\s*h\s*-?[\d.]+\s*z""", RegexOptions.IgnoreCase)]
        private static partial Regex PathRectRegex();
    }
}
