using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Stamps;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LoyaltyCloud.Infrastructure.Services;

internal static class StampStripRenderer
{
    public static IReadOnlyList<StampStripAsset> Render(StampWalletProjection state, string backgroundColor)
    {
        if (state.MaxStamps is < 2 or > 12 || state.CurrentStamps < 0 || state.CurrentStamps > state.MaxStamps)
            throw new InvalidOperationException("Unsupported Stamps visual configuration.");
        var result = new List<StampStripAsset>();
        for (var scale = 1; scale <= 3; scale++)
        {
            using var canvas = new Image<Rgba32>(375 * scale, 144 * scale, ParseColor(backgroundColor));
            var rows = state.MaxStamps <= 5 ? 1 : 2;
            var columns = (int)Math.Ceiling(state.MaxStamps / (double)rows);
            const double gap = 10;
            var size = Math.Min(60, Math.Min((375d - 40 - gap * (columns - 1)) / columns,
                (144d - 24 - gap * (rows - 1)) / rows));
            var gridHeight = rows * size + (rows - 1) * gap;
            for (var index = 0; index < state.MaxStamps; index++)
            {
                var row = index / columns;
                var rowCount = Math.Min(columns, state.MaxStamps - row * columns);
                var rowWidth = rowCount * size + (rowCount - 1) * gap;
                var left = (375 - rowWidth) / 2 + index % columns * (size + gap);
                var top = (144 - gridHeight) / 2 + row * (size + gap);
                var position = index + 1;
                var reward = state.Rewards.SingleOrDefault(x => x.RequiredStamps == position);
                var filled = position <= state.CurrentStamps;
                var kind = reward is not null ? StampImageKind.Reward : filled ? StampImageKind.Filled : StampImageKind.Empty;
                var rewardState = reward?.State ?? (filled ? StampWalletRewardState.Available : StampWalletRewardState.Upcoming);
                var customUpcoming = reward is not null && rewardState == StampWalletRewardState.Upcoming && reward.UpcomingImage is not null;
                var bytes = customUpcoming ? reward!.UpcomingImage : reward?.Image ?? (reward is null ? filled ? state.FilledImage : state.EmptyImage : null);
                var dimension = (int)Math.Round(size * .86 * scale);
                using var source = bytes is null ? CreateDefault(kind, dimension) : Image.Load<Rgba32>(bytes);
                source.Mutate(c => c.Resize(new ResizeOptions { Size = new Size(dimension, dimension), Mode = ResizeMode.Max }));
                if (reward is not null && rewardState != StampWalletRewardState.Available && !customUpcoming) ApplyMutedRewardTreatment(source);
                var x = (int)Math.Round((left + size / 2) * scale - source.Width / 2d);
                var y = (int)Math.Round((top + size / 2) * scale - source.Height / 2d);
                canvas.Mutate(c => c.DrawImage(source, new Point(x, y), 1));
                if (reward is not null && rewardState == StampWalletRewardState.Redeemed)
                    DrawCheck(canvas, (int)Math.Round((left + size * .65) * scale),
                        (int)Math.Round((top + size * .62) * scale), Math.Max(10, (int)(size * .29 * scale)));
            }
            using var output = new MemoryStream(); canvas.SaveAsPng(output);
            result.Add(new StampStripAsset(scale == 1 ? "strip.png" : $"strip@{scale}x.png", output.ToArray()));
        }
        return result;
    }

    internal static void ApplyMutedRewardTreatment(Image<Rgba32> image) =>
        image.Mutate(c => c.Grayscale().Contrast(.55f).Brightness(.75f));

    internal static byte[] UpcomingPreview(byte[] availableImage)
    {
        using var image = Image.Load<Rgba32>(availableImage);
        ApplyMutedRewardTreatment(image);
        using var output = new MemoryStream(); image.SaveAsPng(output); return output.ToArray();
    }

    internal static byte[] DefaultImage(StampImageKind kind)
    {
        using var image = CreateDefault(kind, 128);
        using var output = new MemoryStream(); image.SaveAsPng(output); return output.ToArray();
    }

    private static Image<Rgba32> CreateDefault(StampImageKind kind, int size)
    {
        var image = new Image<Rgba32>(size, size);
        var ink = new Rgba32(45, 58, 64);
        var middle = size / 2d;
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var distance = kind == StampImageKind.Reward ? Math.Abs(x - middle) + Math.Abs(y - middle)
                : Math.Sqrt(Math.Pow(x - middle, 2) + Math.Pow(y - middle, 2));
            if (distance <= size * .47)
                image[x, y] = kind == StampImageKind.Empty && distance < size * .42 ? new Rgba32(235, 239, 241) : ink;
        }
        return image;
    }

    private static void DrawCheck(Image<Rgba32> image, int left, int top, int size)
    {
        // Small neutral disc and white check; independent of tenant branding and source art.
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var u = x / (double)size; var v = y / (double)size;
            var radius = Math.Sqrt(Math.Pow(u - .5, 2) + Math.Pow(v - .5, 2));
            if (radius > .5 || left + x >= image.Width || top + y >= image.Height) continue;
            var check = (u >= .22 && u <= .44 && Math.Abs(v - (u + .23)) < .065)
                || (u >= .40 && u <= .79 && Math.Abs(v - (1.07 - u)) < .065);
            image[left + x, top + y] = radius > .43 || check ? new Rgba32(255, 255, 255) : new Rgba32(35, 43, 48);
        }
    }

    private static Rgba32 ParseColor(string value)
    {
        if (Color.TryParse(value, out var color)) return color.ToPixel<Rgba32>();
        if (value.StartsWith("rgb(", StringComparison.Ordinal) && value.EndsWith(')'))
        {
            var parts = value[4..^1].Split(',');
            if (parts.Length == 3 && byte.TryParse(parts[0].Trim(), out var r)
                && byte.TryParse(parts[1].Trim(), out var g) && byte.TryParse(parts[2].Trim(), out var b)) return new(r, g, b);
        }
        return new(247, 249, 250);
    }
}
