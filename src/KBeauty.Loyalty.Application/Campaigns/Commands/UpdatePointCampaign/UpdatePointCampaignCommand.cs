using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Domain.Enums;
using MediatR;

namespace KBeauty.Loyalty.Application.Campaigns.Commands.UpdatePointCampaign;

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
