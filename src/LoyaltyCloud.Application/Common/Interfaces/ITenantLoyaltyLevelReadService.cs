namespace LoyaltyCloud.Application.Common.Interfaces;

public interface ITenantLoyaltyLevelReadService
{
    Task<IReadOnlyList<TenantLoyaltyLevelDto>> GetActiveLevelsAsync(
        CancellationToken cancellationToken = default);
}

public sealed record TenantLoyaltyLevelDto(
    Guid Id,
    string Name,
    int Threshold,
    int SortOrder);
