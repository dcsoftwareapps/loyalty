namespace KBeauty.Loyalty.Application.Customers.Queries.GetCustomerBySerial;

/// <summary>Vista completa de una clienta para el flujo de escaneo en tienda.</summary>
public sealed record CustomerDetailDto(
    Guid CustomerId,
    string FullName,
    string Email,
    string? Phone,
    DateTime DateOfBirth,
    bool IsBirthMonth,
    string SerialNumber,
    int CurrentPoints,
    int LifetimePoints,
    string Level,
    int PointsToNextLevel,
    int PointsEarnedThisYear,
    DateTime LevelAchievedAt,
    DateTime LastActivityAt,
    DateTime CreatedAt,
    bool IsActive);
