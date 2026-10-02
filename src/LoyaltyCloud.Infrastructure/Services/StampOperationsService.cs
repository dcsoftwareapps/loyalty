using System.Data;
using System.Text.Json;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Stamps;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class StampOperationsService(AppDbContext db, ITenantContext tenantContext,
    ITenantProgramReadService programs, ICurrentUserService currentUser, IDateTimeProvider clock,
    IAppleWalletPassRefreshService refresh, ILogger<StampOperationsService> logger) : IStampOperationsService
{
    public async Task<Result<StampStateDto>> ReadAsync(string serialNumber, CancellationToken ct = default)
    {
        var access = await OperatorAsync(ct);
        if (access.Error is not null) return Result.Fail<StampStateDto>(access.Error);
        var serial = serialNumber?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(serial)) return Result.Fail<StampStateDto>("ID del cliente requerido.");
        if (db.ChangeTracker.HasChanges()) throw new InvalidOperationException("Stamp reads require a clean unit of work.");
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            await LockTenantAsync(ct);
            var card = await CardAsync(serial, ct);
            if (card is null) return Result.Fail<StampStateDto>("Cliente no disponible.");
            var cycle = await LatestCycleAsync(serial, ct);
            if (cycle is null) return Result.Fail<StampStateDto>("El cliente no tiene ciclo de Sellos.");
            var state = await StateAsync(card, cycle, ct);
            await tx.CommitAsync(ct);
            return Result.Ok(state);
        });
    }

    public async Task<Result<StampOperationResult>> ExecuteAsync(string operation, string serialNumber,
        string idempotencyKey, Guid? rewardId = null, CancellationToken ct = default)
    {
        var serial = serialNumber?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(serial) || serial.Length > 20 || string.IsNullOrWhiteSpace(idempotencyKey)
            || idempotencyKey.Length > 100 || idempotencyKey != idempotencyKey.Trim()
            || operation is not ("Earn" or "Reversal" or "Redeem")
            || (operation == "Redeem" && (!rewardId.HasValue || rewardId == Guid.Empty)))
            return Result.Fail<StampOperationResult>("Operacion o clave de idempotencia invalida.");
        var access = await OperatorAsync(ct);
        if (access.Error is not null) return Result.Fail<StampOperationResult>(access.Error);
        var userId = access.UserId;
        var role = access.Role;
        var tenantId = tenantContext.RequireTenantId();
        if (db.ChangeTracker.HasChanges()) throw new InvalidOperationException("Stamp operations require a clean unit of work.");

        var committedResult = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            // Same lock order as configuration. It serializes database writers across app instances.
            await LockTenantAsync(ct);
            var receipt = await db.Set<StampOperationReceipt>().AsNoTracking().SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, ct);
            if (receipt is not null)
            {
                if (receipt.Operation != operation || receipt.SerialNumber != serial || receipt.RewardId != rewardId || receipt.OperatorUserId != userId)
                    return Result.Fail<StampOperationResult>("Conflict: La clave de idempotencia pertenece a otra operacion.");
                return Result.Ok(JsonSerializer.Deserialize<StampOperationResult>(receipt.ResponseJson)!);
            }
            var card = await CardAsync(serial, ct);
            if (card is null) return Result.Fail<StampOperationResult>("Cliente no disponible.");
            var cycle = await LatestCycleAsync(serial, ct);
            if (cycle is null) return Result.Fail<StampOperationResult>("El cliente no tiene ciclo de Sellos.");
            var now = clock.UtcNow;
            var unlocked = new List<EarnedStampReward>();
            var rules = await db.StampProgramVersions.SingleAsync(x => x.Id == cycle.StampProgramVersionId, ct);
            if (operation == "Earn")
            {
                if (cycle.CompletedAt.HasValue)
                {
                    var active = await db.StampProgramVersions.SingleOrDefaultAsync(x => x.IsActive, ct);
                    if (active is null) return Result.Fail<StampOperationResult>("El programa no tiene configuracion activa.");
                    cycle = new StampCycle(Guid.NewGuid(), card, active, now, checked(cycle.Sequence + 1));
                    db.Add(cycle);
                    rules = active;
                }
                cycle.Earn(rules, now);
                var earn = StampTransaction.Earn(cycle, userId, idempotencyKey, await NextSequenceAsync(cycle.Id, ct), now);
                db.Add(earn);
                var definitions = await db.StampRewardDefinitions.Where(x => x.StampProgramVersionId == rules.Id
                    && x.RequiredStamps == cycle.CurrentStamps).ToListAsync(ct);
                foreach (var definition in definitions)
                {
                    var reward = new EarnedStampReward(cycle, earn, definition, now);
                    unlocked.Add(reward); db.Add(reward);
                }
            }
            else if (operation == "Reversal")
            {
                if (rules.ReversalPolicy == StampReversalPolicy.None
                    || (rules.ReversalPolicy == StampReversalPolicy.AdminOnly && role != TenantUserRole.Admin))
                    return Result.Fail<StampOperationResult>("Forbidden: La politica de este ciclo no permite revertir sellos.");
                // Only the newest cycle can be reopened. Never walk back into an earlier completed cycle.
                var earn = await db.StampTransactions.Where(x => x.StampCycleId == cycle.Id && x.Type == StampTransactionType.Earn
                    && !db.StampTransactions.Any(r => r.ReversesTransactionId == x.Id))
                    .OrderByDescending(x => x.Sequence).FirstOrDefaultAsync(ct);
                if (earn is null) return Result.Fail<StampOperationResult>("No hay un Earn efectivo para revertir; Welcome no es reversible.");
                var rewards = await db.Set<EarnedStampReward>().Where(x => x.SourceEarnTransactionId == earn.Id).ToListAsync(ct);
                if (rewards.Any(x => x.Status == EarnedStampRewardStatus.Redeemed))
                    return Result.Fail<StampOperationResult>("Este sello no puede revertirse porque una recompensa obtenida con el ya fue canjeada.");
                foreach (var reward in rewards.Where(x => x.Status == EarnedStampRewardStatus.Available)) reward.Void(userId, now);
                cycle.ReverseEarn();
                db.Add(StampTransaction.Reversal(cycle, userId, idempotencyKey, await NextSequenceAsync(cycle.Id, ct), now, earn));
            }
            else
            {
                var reward = await db.Set<EarnedStampReward>().SingleOrDefaultAsync(x => x.Id == rewardId
                    && db.StampCycles.Any(c => c.Id == x.StampCycleId && c.LoyaltyCardSerialNumber == serial), ct);
                if (reward is null) return Result.Fail<StampOperationResult>("Recompensa no disponible para este cliente.");
                if (reward.Status != EarnedStampRewardStatus.Available)
                    return Result.Fail<StampOperationResult>("Conflict: La recompensa ya no esta disponible.");
                reward.Redeem(userId, now);
            }
            await db.SaveChangesAsync(ct);
            var result = new StampOperationResult(await StateAsync(card, cycle, ct), unlocked.Select(ToRewardDto).ToList());
            db.Add(new StampOperationReceipt(tenantId, idempotencyKey, operation, serial, rewardId, userId,
                JsonSerializer.Serialize(result), now));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result.Ok(result);
        });
        if (committedResult.IsSuccess && operation is "Earn" or "Reversal")
        {
            try
            {
                var cardId = await db.LoyaltyCards.Where(x => x.SerialNumber == serial).Select(x => x.Id).SingleAsync(ct);
                await refresh.RefreshCardAsync(tenantId, cardId,
                    operation == "Earn" ? PassUpdateReason.StampEarned : PassUpdateReason.StampReversed, ct);
            }
            catch (Exception ex) { logger.LogWarning(ex, "Stamps pass refresh failed after committed {Operation}. TenantId={TenantId}", operation, tenantId); }
        }
        return committedResult;
    }

    private async Task<(Guid UserId, TenantUserRole Role, string? Error)> OperatorAsync(CancellationToken ct)
    {
        tenantContext.RequireTenantId();
        if (await programs.GetCurrentAsync(ct) != LoyaltyProgramType.Stamps)
            return (default, default, "Esta operacion requiere un programa de Sellos.");
        if (!currentUser.IsAuthenticated || !Guid.TryParse(currentUser.UserId, out var id))
            return (default, default, "Forbidden: Usuario autenticado requerido.");
        var user = await db.TenantAdminUsers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        if (user is null || user.Role is not (TenantUserRole.Admin or TenantUserRole.Cashier))
            return (default, default, "Forbidden: Operador no autorizado.");
        return (user.Id, user.Role, null);
    }

    private Task<int> LockTenantAsync(CancellationToken ct)
    {
        var tenantId = tenantContext.RequireTenantId();
        return db.Database.ExecuteSqlInterpolatedAsync($"SELECT Id FROM Tenants WITH (UPDLOCK, HOLDLOCK) WHERE Id = {tenantId}", ct);
    }

    private Task<LoyaltyCard?> CardAsync(string serial, CancellationToken ct) => db.LoyaltyCards.SingleOrDefaultAsync(x =>
        x.SerialNumber == serial && x.IsActive && db.Customers.Any(c => c.Id == x.CustomerId && c.IsActive), ct);
    private Task<StampCycle?> LatestCycleAsync(string serial, CancellationToken ct) => db.StampCycles
        .Where(x => x.LoyaltyCardSerialNumber == serial).OrderByDescending(x => x.Sequence).FirstOrDefaultAsync(ct);
    private async Task<long> NextSequenceAsync(Guid cycleId, CancellationToken ct) => checked(
        (await db.StampTransactions.Where(x => x.StampCycleId == cycleId).MaxAsync(x => (long?)x.Sequence, ct) ?? 0) + 1);

    private async Task<StampStateDto> StateAsync(LoyaltyCard card, StampCycle cycle, CancellationToken ct)
    {
        var name = await db.Customers.Where(x => x.Id == card.CustomerId).Select(x => x.FullName).SingleAsync(ct);
        var max = await db.StampProgramVersions.Where(x => x.Id == cycle.StampProgramVersionId).Select(x => x.MaxStamps).SingleAsync(ct);
        var rewards = await db.Set<EarnedStampReward>().AsNoTracking().Where(x => x.Status == EarnedStampRewardStatus.Available
            && db.StampCycles.Any(c => c.Id == x.StampCycleId && c.LoyaltyCardSerialNumber == card.SerialNumber))
            .OrderBy(x => x.EarnedAt).ThenBy(x => x.Id).ToListAsync(ct);
        var rules = await db.StampProgramVersions.AsNoTracking().SingleAsync(x => x.Id == cycle.StampProgramVersionId, ct);
        var access = await OperatorAsync(ct);
        var lastEarn = await db.StampTransactions.Where(x => x.StampCycleId == cycle.Id && x.Type == StampTransactionType.Earn
            && !db.StampTransactions.Any(r => r.ReversesTransactionId == x.Id))
            .OrderByDescending(x => x.Sequence).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        var canReverse = access.Error is null && lastEarn.HasValue
            && (rules.ReversalPolicy == StampReversalPolicy.AdminAndCashier
                || (rules.ReversalPolicy == StampReversalPolicy.AdminOnly && access.Role == TenantUserRole.Admin))
            && !await db.Set<EarnedStampReward>().AnyAsync(x => x.SourceEarnTransactionId == lastEarn.Value
                && x.Status == EarnedStampRewardStatus.Redeemed, ct);
        return new(card.SerialNumber, name, cycle.Id, cycle.Sequence, cycle.CurrentStamps, max,
            cycle.CompletedAt.HasValue, rewards.Select(ToRewardDto).ToList(), canReverse);
    }

    private static StampRewardDto ToRewardDto(EarnedStampReward r) => new(r.Id, r.Name, r.Description, r.RequiredStamps, r.EarnedAt);
}
