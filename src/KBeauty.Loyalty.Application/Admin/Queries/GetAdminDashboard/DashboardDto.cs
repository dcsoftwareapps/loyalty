namespace KBeauty.Loyalty.Application.Admin.Queries.GetAdminDashboard;

/// <summary>Agregaciones para los cards y tablas del dashboard.</summary>
public sealed record DashboardDto(
    int ActiveCustomersCount,
    int PointsIssuedThisMonth,
    int RedemptionsThisMonth,
    IReadOnlyDictionary<string, int> CustomersByLevel,
    IReadOnlyList<RecentVisitDto> RecentVisits);

/// <summary>Fila de la tabla "últimas visitas" del dashboard.</summary>
public sealed record RecentVisitDto(
    Guid TransactionId,
    string CustomerName,
    string SerialNumber,
    string Level,
    int Points,
    decimal? PurchaseAmount,
    DateTime CreatedAt);
