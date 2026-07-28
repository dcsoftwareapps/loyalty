using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Application.Redemptions.Commands.RedeemReward;
using MediatR;

namespace LoyaltyCloud.Application.Redemptions.Commands.RedeemMonetaryDiscount;

public sealed record RedeemMonetaryDiscountCommand(
    string SerialNumber,
    int PointsToRedeem,
    string OperatorId) : IRequest<Result<RedemptionResponse>>;
