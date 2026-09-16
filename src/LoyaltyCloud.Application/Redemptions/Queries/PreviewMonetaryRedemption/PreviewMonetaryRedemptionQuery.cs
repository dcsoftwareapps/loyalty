using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Redemptions.Queries.PreviewMonetaryRedemption;

public sealed record PreviewMonetaryRedemptionQuery(string SerialNumber, int PointsToRedeem)
    : IRequest<Result<MonetaryRedemptionPreviewDto>>;
