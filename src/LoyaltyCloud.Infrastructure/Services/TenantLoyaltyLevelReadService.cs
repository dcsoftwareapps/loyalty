using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class TenantLoyaltyLevelReadService : ITenantLoyaltyLevelReadService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenantContext;

    public TenantLoyaltyLevelReadService(AppDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<TenantLoyaltyLevelDto>> GetActiveLevelsAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.RequireTenantId();
        var levels = await _db.TenantLoyaltyLevels
            .AsNoTracking()
            .Where(level => level.TenantId == tenantId && level.IsActive)
            .OrderBy(level => level.SortOrder)
            .ThenBy(level => level.Threshold)
            .Select(level => new TenantLoyaltyLevelDto(
                level.Id,
                level.Name,
                level.Threshold,
                level.SortOrder))
            .ToListAsync(cancellationToken);

        return levels.AsReadOnly();
    }
}
