using System.Data;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Stamps;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class StampProgramService(AppDbContext db, ITenantContext tenantContext,
    ITenantProgramReadService programs, ICurrentUserService currentUser, IDateTimeProvider clock) : IStampProgramService
{
    public async Task<StampProgramVersion?> GetActiveAsync(CancellationToken ct = default)
    {
        tenantContext.RequireTenantId();
        return await db.Set<StampProgramVersion>().AsNoTracking().SingleOrDefaultAsync(x => x.IsActive, ct);
    }

    public async Task<StampProgramDto?> ReadActiveAsync(CancellationToken ct = default)
    {
        var active = await GetActiveAsync(ct);
        return active is null ? null : await ToDtoAsync(active, ct);
    }

    public async Task<Result<StampProgramDto>> CreateVersionAsync(SetStampProgramCommand command, CancellationToken ct = default)
    {
        var validation = await new SetStampProgramValidator().ValidateAsync(command, ct);
        if (!validation.IsValid) return Result.Fail<StampProgramDto>(string.Join("; ", validation.Errors.Select(x => x.ErrorMessage)));
        var tenantId = tenantContext.RequireTenantId();
        if (await programs.GetCurrentAsync(ct) != LoyaltyProgramType.Stamps)
            return Result.Fail<StampProgramDto>("Esta configuracion requiere un programa de Sellos.");
        if (!currentUser.IsAuthenticated || !Guid.TryParse(currentUser.UserId, out var userId)
            || !await db.TenantAdminUsers.AnyAsync(x => x.Id == userId && x.IsActive && x.Role == TenantUserRole.Admin, ct))
            return Result.Fail<StampProgramDto>("Solo TenantAdmin puede configurar Sellos.");
        if (db.ChangeTracker.HasChanges()) throw new InvalidOperationException("Stamp configuration requires a clean unit of work.");

        // Stable identity allows a retry to recognize a commit whose acknowledgement was lost.
        var versionId = Guid.NewGuid();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            var committed = await db.Set<StampProgramVersion>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == versionId, ct);
            if (committed is not null) return Result.Ok(await ToDtoAsync(committed, ct));
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            // Serialize writers per tenant, including when no version exists yet.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT Id FROM Tenants WITH (UPDLOCK, HOLDLOCK) WHERE Id = {tenantId}", ct);
            var versions = await db.Set<StampProgramVersion>().ToListAsync(ct);
            var now = clock.UtcNow;
            foreach (var previous in versions.Where(x => x.IsActive)) previous.Supersede(now);
            await db.SaveChangesAsync(ct);
            var next = new StampProgramVersion(versionId, tenantId,
                versions.Count == 0 ? 1 : checked(versions.Max(x => x.Version) + 1),
                command.MaxStamps, command.WelcomeStamps, command.ReversalPolicy, userId, now);
            db.Add(next);
            foreach (var reward in command.Rewards)
                db.Add(new StampRewardDefinition(Guid.NewGuid(), next, reward.RequiredStamps, reward.Name, reward.Description));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Result.Ok(await ToDtoAsync(next, ct));
        });
    }

    public void AddInitialCycle(LoyaltyCard card, StampProgramVersion program, DateTime nowUtc)
    {
        if (card.TenantId != tenantContext.RequireTenantId()) throw new InvalidOperationException("Cross-tenant cycle.");
        var cycle = new StampCycle(Guid.NewGuid(), card, program, nowUtc);
        db.Add(cycle);
        if (cycle.CurrentStamps > 0) db.Add(StampTransaction.Welcome(cycle, nowUtc));
    }

    private async Task<StampProgramDto> ToDtoAsync(StampProgramVersion version, CancellationToken ct)
    {
        var rewards = await db.Set<StampRewardDefinition>().AsNoTracking().Where(x => x.StampProgramVersionId == version.Id)
            .OrderBy(x => x.RequiredStamps).Select(x => new StampRewardInput(x.RequiredStamps, x.Name, x.Description)).ToListAsync(ct);
        return new(version.Id, version.Version, version.MaxStamps, version.WelcomeStamps, version.ReversalPolicy, rewards);
    }
}
