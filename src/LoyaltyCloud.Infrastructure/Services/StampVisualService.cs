using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Stamps;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class StampVisualService(AppDbContext db, ITenantContext tenantContext,
    ITenantProgramReadService programs, ICurrentUserService user, IStampVisualAssetStore assets,
    IStampWalletRefreshService refresh, ILogger<StampVisualService> logger) : IStampVisualService
{
    public const int MaxImageBytes = 2 * 1024 * 1024;

    private async Task<bool> AuthorizedAsync(CancellationToken ct)
    {
        tenantContext.RequireTenantId();
        return await programs.GetCurrentAsync(ct) == LoyaltyProgramType.Stamps && user.IsAuthenticated
            && Guid.TryParse(user.UserId, out var id)
            && await db.TenantAdminUsers.AnyAsync(x => x.Id == id && x.IsActive && x.Role == TenantUserRole.Admin, ct);
    }

    public async Task<StampVisualDto> ReadAsync(CancellationToken ct = default)
    {
        if (!await AuthorizedAsync(ct)) throw new UnauthorizedAccessException("Solo TenantAdmin puede configurar Sellos.");
        var tenantId = tenantContext.RequireTenantId();
        var config = await db.StampVisualConfigurations.AsNoTracking().SingleOrDefaultAsync(ct);
        var identities = await db.StampRewardIdentities.AsNoTracking().Where(i => db.StampRewardDefinitions.Any(d =>
            d.RewardIdentityId == i.Id && db.StampProgramVersions.Any(v => v.Id == d.StampProgramVersionId && v.IsActive))).ToListAsync(ct);
        async Task<string> Preview(string? path, StampImageKind kind)
        {
            var bytes = await assets.ReadAsync(tenantId, path, ct) ?? StampStripRenderer.DefaultImage(kind);
            return "data:image/png;base64," + Convert.ToBase64String(bytes);
        }
        var previews = new Dictionary<Guid, string>();
        var upcomingPreviews = new Dictionary<Guid, StampUpcomingRewardPreview>();
        foreach (var identity in identities)
        {
            var available = await assets.ReadAsync(tenantId, identity.ImageBlobName, ct) ?? StampStripRenderer.DefaultImage(StampImageKind.Reward);
            var upcoming = await assets.ReadAsync(tenantId, identity.UpcomingImageBlobName, ct);
            previews[identity.Id] = "data:image/png;base64," + Convert.ToBase64String(available);
            upcomingPreviews[identity.Id] = new("data:image/png;base64," + Convert.ToBase64String(upcoming ?? StampStripRenderer.UpcomingPreview(available)), upcoming is not null);
        }
        return new(await Preview(config?.EmptyStampBlobName, StampImageKind.Empty),
            await Preview(config?.FilledStampBlobName, StampImageKind.Filled), previews) { UpcomingRewardPreviews = upcomingPreviews };
    }

    public async Task<Result> UpdateAsync(StampImageKind kind, Guid? rewardIdentityId, byte[]? image, CancellationToken ct = default)
    {
        if (!await AuthorizedAsync(ct)) return Result.Fail("Solo TenantAdmin puede configurar Sellos.");
        var rewardImage = kind is StampImageKind.Reward or StampImageKind.UpcomingReward;
        if (!Enum.IsDefined(kind) || rewardImage != rewardIdentityId.HasValue)
            return Result.Fail("Tipo de imagen inválido.");
        var identity = rewardImage
            ? await db.StampRewardIdentities.SingleOrDefaultAsync(x => x.Id == rewardIdentityId, ct) : null;
        if (rewardImage && (identity is null || !await db.StampRewardDefinitions.AnyAsync(d =>
            d.RewardIdentityId == identity.Id && db.StampProgramVersions.Any(v => v.Id == d.StampProgramVersionId && v.IsActive), ct)))
            return Result.Fail("Recompensa no disponible en la configuración activa.");
        var config = rewardImage ? null : await db.StampVisualConfigurations.SingleOrDefaultAsync(ct);
        byte[]? png = null;
        if (image is not null)
        {
            if (image.Length == 0 || image.Length > MaxImageBytes) return Result.Fail("La imagen debe pesar máximo 2 MB.");
            try { png = NormalizeImage(image); }
            catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or ArgumentException)
            { return Result.Fail("Usa una imagen PNG o JPG válida, de hasta 4096 × 4096 px."); }
        }
        var tenantId = tenantContext.RequireTenantId();
        var currentBlob = kind switch
        {
            StampImageKind.Reward => identity!.ImageBlobName,
            StampImageKind.UpcomingReward => identity!.UpcomingImageBlobName,
            StampImageKind.Filled => config?.FilledStampBlobName,
            _ => config?.EmptyStampBlobName
        };
        var unchanged = png is null && currentBlob is null;
        if (!unchanged && png is not null && currentBlob is not null)
        {
            var currentImage = await assets.ReadAsync(tenantId, currentBlob, ct);
            unchanged = currentImage is not null && png.AsSpan().SequenceEqual(currentImage);
        }
        if (!unchanged)
        {
            var blob = png is null ? null : await assets.UploadAsync(tenantId, png, ct);
            if (identity is not null)
            {
                if (kind == StampImageKind.UpcomingReward) identity.SetUpcomingImage(blob);
                else identity.SetImage(blob);
            }
            else
            {
                if (config is null) { config = new(tenantId); db.Add(config); }
                config.SetImage(kind == StampImageKind.Filled, blob);
            }
        }
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT Id FROM Tenants WITH (UPDLOCK, HOLDLOCK) WHERE Id = {tenantId}", ct);
            foreach (var context in await db.StampPassEventContexts.Where(x => x.AnnouncementEnabled).ToListAsync(ct)) context.Suppress();
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        // An unchanged image still suppresses stale Wallet event context, as before this fix.
        if (unchanged) return Result.Ok();
        // The visual change is committed before either Wallet provider is contacted.
        try
        {
            var cards = await AffectedInstalledCardsAsync(tenantId, rewardIdentityId, ct);
            foreach (var card in cards)
            {
                try { await refresh.RefreshCardAsync(tenantId, card.Id, card.SerialNumber, PassUpdateReason.BrandingUpdated, ct); }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Stamps visual refresh failed after saving assets. TenantId={TenantId}, Serial={Serial}", tenantId, card.SerialNumber);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Stamps visual refresh could not enumerate installed passes. TenantId={TenantId}", tenantId);
        }
        return Result.Ok();
    }

    private async Task<List<(Guid Id, string SerialNumber)>> AffectedInstalledCardsAsync(
        Guid tenantId, Guid? rewardIdentityId, CancellationToken ct)
    {
        var cards = db.LoyaltyCards.AsNoTracking().Where(card => card.TenantId == tenantId && card.IsActive
            && db.Customers.Any(customer => customer.Id == card.CustomerId && customer.IsActive)
            && (db.DeviceRegistrations.Any(device => device.TenantId == tenantId && device.SerialNumber == card.SerialNumber)
                || db.MemberDigitalWallets.Any(wallet => wallet.TenantId == tenantId
                    && wallet.LoyaltyCardId == card.Id && wallet.Provider == DigitalWalletProvider.Google
                    && wallet.Status != DigitalWalletStatus.Inactive)));

        if (rewardIdentityId is Guid identityId)
            cards = cards.Where(card => db.StampCycles.Any(cycle => cycle.TenantId == tenantId
                && cycle.LoyaltyCardSerialNumber == card.SerialNumber
                && !db.StampCycles.Any(newer => newer.TenantId == tenantId
                    && newer.LoyaltyCardSerialNumber == card.SerialNumber && newer.Sequence > cycle.Sequence)
                && db.StampRewardDefinitions.Any(reward => reward.TenantId == tenantId
                    && reward.StampProgramVersionId == cycle.StampProgramVersionId
                    && reward.RewardIdentityId == identityId)));
        else
            cards = cards.Where(card => db.StampCycles.Any(cycle => cycle.TenantId == tenantId
                && cycle.LoyaltyCardSerialNumber == card.SerialNumber));

        var affected = await cards.OrderBy(card => card.SerialNumber)
            .Select(card => new { card.Id, card.SerialNumber }).ToListAsync(ct);
        return affected.Select(card => (card.Id, card.SerialNumber)).ToList();
    }

    internal static byte[] NormalizeImage(byte[] bytes)
    {
        var format = Image.DetectFormat(bytes);
        if (format.Name is not ("PNG" or "JPEG")) throw new ArgumentException("Unsupported image format.");
        var info = Image.Identify(bytes);
        if (info.Width > 4096 || info.Height > 4096) throw new ArgumentException("Image dimensions exceeded.");
        using var image = Image.Load<Rgba32>(new DecoderOptions { MaxFrames = 1 }, bytes);
        image.Mutate(x => x.AutoOrient().Resize(new ResizeOptions { Size = new Size(1024, 1024), Mode = ResizeMode.Max }));
        image.Metadata.ExifProfile = null;
        using var output = new MemoryStream(); image.SaveAsPng(output); return output.ToArray();
    }
}
