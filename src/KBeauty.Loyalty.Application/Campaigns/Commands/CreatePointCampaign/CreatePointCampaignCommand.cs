using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Domain.Enums;
using MediatR;

namespace KBeauty.Loyalty.Application.Campaigns.Commands.CreatePointCampaign;

public sealed record CreatePointCampaignCommand(
    string Name,
    string Description,
    int Multiplier,
    decimal? MinimumPurchaseAmount,
    CampaignLevelEligibility LevelEligibility,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    bool IsActive = true) : IRequest<Result<PointCampaignAdminDto>>;
