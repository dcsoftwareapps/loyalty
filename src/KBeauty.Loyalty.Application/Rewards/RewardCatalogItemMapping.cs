using KBeauty.Loyalty.Domain.Entities;

namespace KBeauty.Loyalty.Application.Rewards;

internal static class RewardCatalogItemMapping
{
    public static RewardAdminDto ToAdminDto(this RewardCatalogItem item, DateTime nowUtc) =>
        new(
            Id: item.Id,
            Name: item.Name,
            Description: item.Description,
            PointsCost: item.PointsCost,
            MinLevel: item.MinLevel,
            IsActive: item.IsActive,
            IsMonthlyProduct: item.IsMonthlyProduct,
            ValidFrom: item.ValidFrom,
            ValidTo: item.ValidTo,
            IsCurrentlyAvailable: item.IsAvailableOn(nowUtc));
}
