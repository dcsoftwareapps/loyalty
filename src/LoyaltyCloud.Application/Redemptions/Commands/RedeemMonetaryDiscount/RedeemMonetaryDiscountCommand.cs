using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Application.Redemptions.Commands.RedeemReward;
using MediatR;

namespace LoyaltyCloud.Application.Redemptions.Commands.RedeemMonetaryDiscount;

public sealed record RedeemMonetaryDiscountCommand(
    string SerialNumber,
    int PointsToRedeem,
    string OperatorId,
    string? IdempotencyKey = null,
    decimal? ExpectedMonetaryAmount = null,
    string? ExpectedMonetaryCurrency = null,
    decimal? ExpectedPointsPerPesoUnit = null) : IRequest<Result<RedemptionResponse>>;
