namespace LoyaltyCloud.Application.Admin.Queries.GetReportsSummary;

public sealed record ReportsSummaryDto(
    ReportsPeriodDto Period,
    ReportsPeriodMetricsDto PeriodMetrics,
    ReportsCurrentProgramMetricsDto CurrentProgram,
    ReportsInactiveCustomersDto InactiveCustomers,
    IReadOnlyList<ReportsTopRewardDto> TopRewards);

public sealed record ReportsPeriodDto(
    DateTime StartUtc,
    DateTime EndUtc,
    int InactiveDaysThreshold);

public sealed record ReportsPeriodMetricsDto(
    int NewCustomers,
    int ActiveCustomers,
    int RegisteredPurchases,
    decimal RegisteredPurchaseAmount,
    int PointsIssued,
    int PointsRedeemed,
    int PointsExpired,
    int Redemptions);

public sealed record ReportsCurrentProgramMetricsDto(
    int TotalCustomers,
    int CurrentPointBalance,
    int AppleWalletRegistrations,
    int GoogleWalletRecords);

public sealed record ReportsInactiveCustomersDto(
    int ThresholdDays,
    int Total,
    IReadOnlyList<ReportsInactiveCustomerDto> Items);

public sealed record ReportsInactiveCustomerDto(
    Guid CustomerId,
    string CustomerName,
    string SerialNumber,
    string CurrentLevel,
    int CurrentPoints,
    DateTime LastActivityUtc,
    int DaysWithoutActivity);

public sealed record ReportsTopRewardDto(
    string RewardName,
    int Redemptions);
