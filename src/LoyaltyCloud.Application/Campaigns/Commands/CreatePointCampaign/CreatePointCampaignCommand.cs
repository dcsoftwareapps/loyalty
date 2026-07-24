using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Campaigns.Commands.CreatePointCampaign;

public sealed record CreatePointCampaignCommand(
    string Name,
    string Description,
    int Multiplier,
    decimal? MinimumPurchaseAmount,
    string LevelEligibility,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    bool IsActive = true) : IRequest<Result<PointCampaignAdminDto>>;
