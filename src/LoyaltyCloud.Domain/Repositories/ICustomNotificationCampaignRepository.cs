using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Domain.Repositories;

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
