using KBeauty.Loyalty.Domain.Entities;
using KBeauty.Loyalty.Domain.Enums;

namespace KBeauty.Loyalty.Domain.Repositories;

public interface IPointCampaignRepository
{
    Task<IReadOnlyList<PointCampaign>> GetAllAsync(CancellationToken ct = default);
    Task<PointCampaign?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PointCampaign?> GetBestApplicableAsync(
        DateTime nowUtc,
        decimal purchaseAmount,
        string loyaltyLevel,
        CancellationToken ct = default);
    Task AddAsync(PointCampaign campaign, CancellationToken ct = default);
    void Update(PointCampaign campaign);
}
