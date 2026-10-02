using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Stamps;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class StampWalletProjectionService(AppDbContext db, ITenantContext context,
    IStampVisualAssetStore assets) : IStampWalletProjectionService
{
    public async Task<StampWalletProjection> ReadAsync(string serialNumber, CancellationToken ct = default)
    {
        var tenantId = context.RequireTenantId();
        if (!await db.LoyaltyCards.AsNoTracking().AnyAsync(c => c.SerialNumber == serialNumber && c.IsActive
            && db.Customers.Any(x => x.Id == c.CustomerId && x.IsActive), ct))
            throw new InvalidOperationException("Cliente no disponible.");
        var cycle = await db.StampCycles.AsNoTracking().Where(x => x.LoyaltyCardSerialNumber == serialNumber)
            .OrderByDescending(x => x.Sequence).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("El cliente no tiene ciclo de Sellos.");
        var version = await db.StampProgramVersions.AsNoTracking().SingleAsync(x => x.Id == cycle.StampProgramVersionId, ct);
        var config = await db.StampVisualConfigurations.AsNoTracking().SingleOrDefaultAsync(ct);
        var definitions = await db.StampRewardDefinitions.AsNoTracking().Where(x => x.StampProgramVersionId == version.Id).ToListAsync(ct);
        var ids = definitions.Select(x => x.RewardIdentityId).ToList();
        var identities = await db.StampRewardIdentities.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var rewards = new List<StampWalletReward>();
        foreach (var definition in definitions)
            rewards.Add(new(definition.RequiredStamps, definition.RewardIdentityId,
                await assets.ReadAsync(tenantId, identities[definition.RewardIdentityId].ImageBlobName, ct)));
        return new(cycle.Id, version.Id, cycle.CurrentStamps, version.MaxStamps,
            await assets.ReadAsync(tenantId, config?.EmptyStampBlobName, ct),
            await assets.ReadAsync(tenantId, config?.FilledStampBlobName, ct), rewards);
    }

    public IReadOnlyList<StampStripAsset> Render(StampWalletProjection projection, string backgroundColor)
        => StampStripRenderer.Render(projection, backgroundColor);
}
