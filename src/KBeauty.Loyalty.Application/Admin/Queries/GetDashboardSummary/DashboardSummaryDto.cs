namespace KBeauty.Loyalty.Application.Admin.Queries.GetDashboardSummary;

public sealed record DashboardSummaryDto(
    DashboardCustomerMetricsDto Customers,
    DashboardPointMetricsDto Points,
    DashboardRedemptionMetricsDto Redemptions,
    DashboardRewardMetricsDto Rewards,
    IReadOnlyList<DashboardRecentActivityItemDto> RecentActivity);

public sealed record DashboardCustomerMetricsDto(
    int TotalCustomers,
    int NewCustomersThisMonth,
    int CustomersWithWallet,
    int ActiveCustomers);

public sealed record DashboardPointMetricsDto(
    int PointsIssued,
    int PointsRedeemed,
    int PointsExpired,
    int CurrentPointBalance);

public sealed record DashboardRedemptionMetricsDto(
    int Pending,
    int Confirmed,
    int Cancelled,
    int Total);

public sealed record DashboardRewardMetricsDto(
    int Total,
    int Active,
    int Inactive);

public sealed record DashboardRecentActivityItemDto(
    string Type,
    string Description,
    string CustomerName,
    string SerialNumber,
    int Points,
    string? Status,
    DateTime OccurredAt);
