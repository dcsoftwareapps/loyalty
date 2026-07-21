using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Domain.Enums;
using MediatR;

namespace LoyaltyCloud.Application.Campaigns.Commands.UpdatePointCampaign;

public sealed record UpdatePointCampaignCommand(
    Guid Id,
    string Name,
    string Description,
    int Multiplier,
    decimal? MinimumPurchaseAmount,
    CampaignLevelEligibility LevelEligibility,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    bool IsActive = true) : IRequest<Result<PointCampaignAdminDto>>;
