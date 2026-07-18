using KBeauty.Loyalty.Domain.Entities;
using KBeauty.Loyalty.Domain.Enums;

namespace KBeauty.Loyalty.Domain.Repositories;

public interface ICustomNotificationCampaignRepository
{
    Task<CustomNotificationCampaign?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CustomNotificationCampaign>> ListAsync(
        CustomNotificationCampaignStatus? status,
        int take,
        CancellationToken ct = default);
    Task<IReadOnlyList<CustomNotificationCampaign>> GetDueAsync(DateTime nowUtc, int take, CancellationToken ct = default);
    Task AddAsync(CustomNotificationCampaign campaign, CancellationToken ct = default);
    void Update(CustomNotificationCampaign campaign);
}
