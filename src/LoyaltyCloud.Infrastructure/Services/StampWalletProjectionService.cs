using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Stamps;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Common.Services;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class StampWalletProjectionService(AppDbContext db, ITenantContext context,
    IStampVisualAssetStore assets, IDateTimeProvider clock) : IStampWalletProjectionService
{
    public async Task<StampWalletProjection> ReadAsync(string serialNumber, CancellationToken ct = default)
    {
        var tenantId = context.RequireTenantId();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // Read cycle, rewards and announcement as one coherent snapshot against economic writers.
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT Id FROM Tenants WITH (UPDLOCK, HOLDLOCK) WHERE Id = {tenantId}", ct);
            var result = await ReadCoreAsync(serialNumber, ct);
            await tx.CommitAsync(ct);
            return result;
        });
    }

    private async Task<StampWalletProjection> ReadCoreAsync(string serialNumber, CancellationToken ct)
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
        var earned = await db.EarnedStampRewards.AsNoTracking().Where(x => x.StampCycleId == cycle.Id
            && x.Status != EarnedStampRewardStatus.Voided).ToListAsync(ct);
        var rewards = new List<StampWalletReward>();
        foreach (var definition in definitions)
            rewards.Add(new(definition.RequiredStamps, definition.RewardIdentityId,
                await assets.ReadAsync(tenantId, identities[definition.RewardIdentityId].ImageBlobName, ct),
                earned.Any(x => x.StampRewardDefinitionId == definition.Id && x.Status == EarnedStampRewardStatus.Available)
                    ? StampWalletRewardState.Available
                    : earned.Any(x => x.StampRewardDefinitionId == definition.Id && x.Status == EarnedStampRewardStatus.Redeemed)
                        ? StampWalletRewardState.Redeemed : StampWalletRewardState.Upcoming,
                await assets.ReadAsync(tenantId, identities[definition.RewardIdentityId].UpcomingImageBlobName, ct)));
        var eventContext = await db.StampPassEventContexts.AsNoTracking().SingleOrDefaultAsync(x =>
            x.SerialNumber == serialNumber && x.AnnouncementEnabled && x.ExpiresAtUtc > clock.UtcNow, ct);
        StampWalletEvent? passEvent = null;
        if (eventContext is not null)
        {
            var transaction = await db.StampTransactions.AsNoTracking().Where(x => x.StampCycleId == cycle.Id)
                .OrderByDescending(x => x.Sequence).FirstOrDefaultAsync(ct);
            if (transaction?.Id == eventContext.TransactionId)
            {
                var unlocked = earned.Where(x => x.SourceEarnTransactionId == transaction.Id
                    && x.Status == EarnedStampRewardStatus.Available).OrderBy(x => x.RequiredStamps).ThenBy(x => x.Id).ToList();
                var rewardUnlocked = transaction.Type == StampTransactionType.Earn && unlocked.Count > 0;
                var value = rewardUnlocked ? string.Join(", ", unlocked.Select(x => x.ShortName)) : $"{cycle.CurrentStamps} / {version.MaxStamps}";
                passEvent = new(transaction.Id, transaction.Type.ToString(), eventContext.IsRollover, value,
                    rewardUnlocked ? $"🎁 ¡Desbloqueaste {value.Replace("%", "%%")}! Ahora tienes %@." : transaction.Type == StampTransactionType.Reversal
                        ? "Se canceló el último sello. Ahora tienes %@." : "🎉 ¡Conseguiste un sello más! Ahora tienes %@.", rewardUnlocked);
            }
        }
        return new(cycle.Id, version.Id, cycle.CurrentStamps, version.MaxStamps,
            await assets.ReadAsync(tenantId, config?.EmptyStampBlobName, ct),
            await assets.ReadAsync(tenantId, config?.FilledStampBlobName, ct), rewards, passEvent);
    }

    public IReadOnlyList<StampStripAsset> Render(StampWalletProjection projection, string backgroundColor)
        => StampStripRenderer.Render(projection, backgroundColor);
}
