namespace LoyaltyCloud.Application.Redemptions.Queries.PreviewMonetaryRedemption;

public sealed record MonetaryRedemptionPreviewDto(
    string SerialNumber,
    int PointsToRedeem,
    decimal MonetaryAmount,
    string MonetaryCurrency,
    decimal MonetaryPointsPerPesoUnit,
    int CurrentPoints,
    int RemainingPoints);
