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
    IAppleWalletPassRefreshService refresh, ILogger<StampVisualService> logger) : IStampVisualService
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
        foreach (var identity in identities) previews[identity.Id] = await Preview(identity.ImageBlobName, StampImageKind.Reward);
        return new(await Preview(config?.EmptyStampBlobName, StampImageKind.Empty),
            await Preview(config?.FilledStampBlobName, StampImageKind.Filled), previews);
    }

    public async Task<Result> UpdateAsync(StampImageKind kind, Guid? rewardIdentityId, byte[]? image, CancellationToken ct = default)
    {
        if (!await AuthorizedAsync(ct)) return Result.Fail("Solo TenantAdmin puede configurar Sellos.");
        if (!Enum.IsDefined(kind) || (kind == StampImageKind.Reward) != rewardIdentityId.HasValue)
            return Result.Fail("Tipo de imagen inválido.");
        var identity = kind == StampImageKind.Reward
            ? await db.StampRewardIdentities.SingleOrDefaultAsync(x => x.Id == rewardIdentityId, ct) : null;
        if (kind == StampImageKind.Reward && (identity is null || !await db.StampRewardDefinitions.AnyAsync(d =>
            d.RewardIdentityId == identity.Id && db.StampProgramVersions.Any(v => v.Id == d.StampProgramVersionId && v.IsActive), ct)))
            return Result.Fail("Recompensa no disponible en la configuración activa.");
        byte[]? png = null;
        if (image is not null)
        {
            if (image.Length == 0 || image.Length > MaxImageBytes) return Result.Fail("La imagen debe pesar máximo 2 MB.");
            try { png = NormalizeImage(image); }
            catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or ArgumentException)
            { return Result.Fail("Usa una imagen PNG o JPG válida, de hasta 4096 × 4096 px."); }
        }
        var tenantId = tenantContext.RequireTenantId();
        var blob = png is null ? null : await assets.UploadAsync(tenantId, png, ct);
        if (identity is not null) identity.SetImage(blob);
        else
        {
            var config = await db.StampVisualConfigurations.SingleOrDefaultAsync(ct);
            if (config is null) { config = new(tenantId); db.Add(config); }
            config.SetImage(kind == StampImageKind.Filled, blob);
        }
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT Id FROM Tenants WITH (UPDLOCK, HOLDLOCK) WHERE Id = {tenantId}", ct);
            foreach (var context in await db.StampPassEventContexts.Where(x => x.AnnouncementEnabled).ToListAsync(ct)) context.Suppress();
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        // SQL is committed before external best-effort APNs work; no economic version is created.
        try { await refresh.RefreshTenantInstalledPassesAsync(tenantId, PassUpdateReason.BrandingUpdated, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Stamps visual refresh failed after saving assets. TenantId={TenantId}", tenantId); }
        return Result.Ok();
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
