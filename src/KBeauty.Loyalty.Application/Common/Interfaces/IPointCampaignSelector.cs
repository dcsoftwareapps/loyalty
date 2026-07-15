namespace KBeauty.Loyalty.Application.Common.Interfaces;

public interface IPointCampaignSelector
{
    Task<SelectedPointCampaign?> SelectBestAsync(
        DateTime nowUtc,
        decimal purchaseAmount,
        string loyaltyLevel,
        CancellationToken ct = default);
}

public sealed record SelectedPointCampaign(
    Guid Id,
    string Name,
    int Multiplier);
