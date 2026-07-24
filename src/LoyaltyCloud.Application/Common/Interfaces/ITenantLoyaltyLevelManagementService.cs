using LoyaltyCloud.Application.Levels;
using LoyaltyCloud.Common.Results;

namespace LoyaltyCloud.Application.Common.Interfaces;

public interface ITenantLoyaltyLevelManagementService
{
    Task<Result<IReadOnlyList<TenantLoyaltyLevelAdminDto>>> ListAsync(CancellationToken ct = default);

    Task<Result<UpdateTenantLoyaltyLevelsResultDto>> UpdateAsync(
        IReadOnlyList<TenantLoyaltyLevelUpdateItemDto> levels,
        string operatorId,
        CancellationToken ct = default);
}
