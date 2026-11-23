using System.Globalization;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LoyaltyCloud.Tools;

// Offline visual experiment only; deliberately independent of Wallet services and DI.
internal static class StampWalletPrototype
{
    private const int Width = 375;
    private const int Height = 144;
    private static readonly Rgba32 Background = new(247, 249, 250);
    private static readonly Rgba32 Ink = new(45, 58, 64);
    private static readonly Rgba32 Muted = new(164, 175, 181);

    public static void Generate(string outputDirectory)
    {
        var output = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(output);
        var html = new StringBuilder("""
            <!doctype html><html lang="es"><meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <title>Stamps - experimento de strip</title>
            <style>
            *{box-sizing:border-box}body{font:15px system-ui;margin:24px;color:#2d3a40;background:#fff}
            main{max-width:1240px;margin:auto}h1{font-size:24px}h2{font-size:20px;margin-top:32px}
            .samples{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,375px),1fr));gap:20px}
            figure{margin:0}figcaption{margin-bottom:8px}img{display:block;width:375px;max-width:100%;height:auto}
            p{line-height:1.5}a{color:#2d3a40}small{display:block;margin-top:8px}
            </style><main><h1>Stamps: strip Apple Wallet</h1>
            <p>Experimento local, sin pase firmado ni datos reales. Las muestras se muestran a 375 x 144;
            abre el PNG 3x para inspeccionar el detalle. Circulo vacio: pendiente; circulo lleno: ganado.
            Rombo con candado: recompensa pendiente; rombo oscuro con check: recompensa alcanzada.</p>
            """);
        var metrics = new StringBuilder("total,progress,layout,rows,columns,slot_px,gap_px,path\n");
        var cases = new[] { 5, 8, 10, 12, 15 };
        foreach (var total in cases)
        {
            var progressCases = total == 10 ? new[] { 0, 4, 5, 7, 10 } : new[] { 0, total / 2, total };
            html.Append($"<h2>{total} sellos</h2><div class=\"samples\">");
            foreach (var progress in progressCases)
                WriteSample(output, total, progress, total == 5 ? 1 : 2, "balanced", html, metrics);
            html.Append("</div>");
        }
        html.Append("<h2>Alternativas de densidad</h2><div class=\"samples\">");
        foreach (var total in cases.Where(total => total > 5))
            WriteSample(output, total, total / 2, 1, "one-row", html, metrics);
        WriteSample(output, 15, 7, 3, "three-rows", html, metrics);
        html.Append("</div></main></html>");
        File.WriteAllText(Path.Combine(output, "index.html"), html.ToString());
        File.WriteAllText(Path.Combine(output, "metrics.csv"), metrics.ToString());
        Console.WriteLine($"Generated 22 samples / 66 PNGs: {output}");
        Console.WriteLine($"Comparison: {Path.Combine(output, "index.html")}");
    }

    private static void WriteSample(string output, int total, int progress, int rows, string variant,
        StringBuilder html, StringBuilder metrics)
    {
        var columns = (int)Math.Ceiling(total / (double)rows);
        var gap = rows == 3 ? 8d : 10d;
        var size = Math.Min(60, Math.Min((Width - 40d - gap * (columns - 1)) / columns,
            (Height - 24d - gap * (rows - 1)) / rows));
        var relative = $"{total:D2}-stamps/{progress:D2}-of-{total:D2}-{variant}";
        var directory = Path.Combine(output, relative);
        Directory.CreateDirectory(directory);
        using var highResolution = Render(total, progress, rows, columns, size, gap);
        foreach (var scale in new[] { 1, 2, 3 })
        {
            using var image = highResolution.Clone(context => context.Resize(Width * scale, Height * scale));
            var name = scale == 1 ? "strip.png" : $"strip@{scale}x.png";
            image.SaveAsPng(Path.Combine(directory, name));
        }
        var sizeText = size.ToString("0.0", CultureInfo.InvariantCulture);
        html.Append($"<figure><figcaption>{progress}/{total} · {variant} · {rows} filas · {sizeText} px</figcaption>"
            + $"<a href=\"{relative}/strip@3x.png\"><img src=\"{relative}/strip@3x.png\" width=\"375\" height=\"144\" alt=\"{progress} de {total} sellos\"></a>"
            + $"<small>Recompensas: posiciones {(total + 1) / 2} y {total}</small></figure>");
        metrics.Append(CultureInfo.InvariantCulture,
            $"{total},{progress},{variant},{rows},{columns},{size:0.00},{gap:0.00},{relative}\n");
    }

    private static Image<Rgba32> Render(int total, int progress, int rows, int columns, double size, double gap)
    {
        var image = new Image<Rgba32>(Width * 3, Height * 3, Background);
        var gridHeight = rows * size + (rows - 1) * gap;
        for (var index = 0; index < total; index++)
        {
            var row = index / columns;
            var rowCount = Math.Min(columns, total - row * columns);
            var rowWidth = rowCount * size + (rowCount - 1) * gap;
            var x = (Width - rowWidth) / 2 + (index % columns) * (size + gap) + size / 2;
            var y = (Height - gridHeight) / 2 + row * (size + gap) + size / 2;
            var position = index + 1;
            var reward = position == (total + 1) / 2 || position == total;
            DrawSlot(image, x, y, size, reward, position <= progress);
        }
        return image;
    }

    private static void DrawSlot(Image<Rgba32> image, double x, double y, double size, bool reward, bool filled)
    {
        var radius = size * .43;
        var border = Math.Max(1.25, size * .035);
        Paint(image, x - size / 2, y - size / 2, size, size, (px, py) =>
        {
            var distance = reward ? Math.Abs(px - x) + Math.Abs(py - y)
                : Math.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
            return distance <= radius
                ? filled ? Ink : distance >= radius - border ? Muted : new Rgba32(229, 235, 238)
                : null;
        });
        if (filled)
        {
            Line(image, x - size * .16, y, x - size * .03, y + size * .12, size * .055, Color.White.ToPixel<Rgba32>());
            Line(image, x - size * .03, y + size * .12, x + size * .20, y - size * .14, size * .055, Color.White.ToPixel<Rgba32>());
        }
        else if (reward)
        {
            // One reward asset plus an overlaid lock; no Unicode/font dependency.
            var lockWidth = size * .26;
            Paint(image, x - lockWidth / 2, y - size * .02, lockWidth, size * .19, (_, _) => Ink);
            Paint(image, x - size * .10, y - size * .20, size * .20, size * .20, (px, py) =>
            {
                var distance = Math.Sqrt((px - x) * (px - x) + (py - (y - size * .10)) * (py - (y - size * .10)));
                return py <= y - size * .02 && distance <= size * .10 && distance >= size * .06 ? Ink : null;
            });
        }
    }

    private static void Line(Image<Rgba32> image, double x1, double y1, double x2, double y2, double thickness, Rgba32 color)
    {
        var left = Math.Min(x1, x2) - thickness;
        var top = Math.Min(y1, y2) - thickness;
        Paint(image, left, top, Math.Abs(x2 - x1) + thickness * 2, Math.Abs(y2 - y1) + thickness * 2, (px, py) =>
        {
            var dx = x2 - x1;
            var dy = y2 - y1;
            var t = Math.Clamp(((px - x1) * dx + (py - y1) * dy) / (dx * dx + dy * dy), 0, 1);
            var distance = Math.Sqrt(Math.Pow(px - x1 - t * dx, 2) + Math.Pow(py - y1 - t * dy, 2));
            return distance <= thickness / 2 ? color : null;
        });
    }

    private static void Paint(Image<Rgba32> image, double x, double y, double width, double height,
        Func<double, double, Rgba32?> pixel)
    {
        for (var py = Math.Max(0, (int)Math.Floor(y * 3)); py < Math.Min(image.Height, (int)Math.Ceiling((y + height) * 3)); py++)
        for (var px = Math.Max(0, (int)Math.Floor(x * 3)); px < Math.Min(image.Width, (int)Math.Ceiling((x + width) * 3)); px++)
        {
            var color = pixel((px + .5) / 3, (py + .5) / 3);
            if (color.HasValue)
                image[px, py] = color.Value;
        }
    }
}
