using LoyaltyCloud.Domain.ValueObjects;

namespace LoyaltyCloud.Application.Redemptions;

internal static class MonetaryRedemptionCalculator
{
    public const string Currency = "MXN";

    public static MonetaryRedemptionCalculation Calculate(int pointsToRedeem, ProgramConfigSnapshot config)
    {
        var pointUnit = GetPointUnit(config);
        if (pointUnit is null)
            return MonetaryRedemptionCalculation.Invalid("La configuracion de canje no es valida.");

        if (pointsToRedeem <= 0)
            return MonetaryRedemptionCalculation.Invalid("Los puntos a canjear deben ser mayores a 0.");

        if (pointsToRedeem % pointUnit.Value != 0)
            return MonetaryRedemptionCalculation.Invalid($"Los puntos deben canjearse en multiplos de {pointUnit.Value:N0}.");

        var amount = decimal.Round(pointsToRedeem / config.PointsPerPesoUnit, 2, MidpointRounding.AwayFromZero);
        if (amount <= 0)
            return MonetaryRedemptionCalculation.Invalid("El descuento calculado debe ser mayor a $0.00 MXN.");

        return MonetaryRedemptionCalculation.Valid(pointsToRedeem, amount, Currency, config.PointsPerPesoUnit, pointUnit.Value);
    }

    public static int CalculateUsablePoints(int currentPoints, ProgramConfigSnapshot config)
    {
        var pointUnit = GetPointUnit(config);
        if (currentPoints <= 0 || pointUnit is null)
            return 0;

        return currentPoints - (currentPoints % pointUnit.Value);
    }

    private static int? GetPointUnit(ProgramConfigSnapshot config)
    {
        if (config.PointsPerPesoUnit <= 0)
            return null;

        if (decimal.Truncate(config.PointsPerPesoUnit) != config.PointsPerPesoUnit)
            return null;

        if (config.PointsPerPesoUnit > int.MaxValue)
            return null;

        return (int)config.PointsPerPesoUnit;
    }
}

internal sealed record MonetaryRedemptionCalculation(
    bool IsValid,
    string? Error,
    int Points,
    decimal Amount,
    string Currency,
    decimal PointsPerPesoUnit,
    int PointUnit)
{
    public static MonetaryRedemptionCalculation Valid(
        int points,
        decimal amount,
        string currency,
        decimal pointsPerPesoUnit,
        int pointUnit) =>
        new(true, null, points, amount, currency, pointsPerPesoUnit, pointUnit);

    public static MonetaryRedemptionCalculation Invalid(string error) =>
        new(false, error, 0, 0, MonetaryRedemptionCalculator.Currency, 0, 0);
}
