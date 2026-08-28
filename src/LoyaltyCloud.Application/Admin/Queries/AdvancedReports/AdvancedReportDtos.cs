namespace LoyaltyCloud.Application.Admin.Queries.AdvancedReports;

public enum TopCustomerMetric { PointsEarned, PointsRedeemed, Activity, PurchaseAmount }

public sealed record TopCustomersReportDto(DateTime StartUtc, DateTime EndUtc, TopCustomerMetric Metric, IReadOnlyList<TopCustomerRowDto> Customers);
public sealed record TopCustomerRowDto(Guid CustomerId, string CustomerName, string Level, int ActivityCount, int PointsEarned, int PointsRedeemed, int Redemptions, decimal PurchaseAmount, decimal RankingValue);

public sealed record VisitFrequencyReportDto(DateTime StartUtc, DateTime EndUtc, decimal? AverageDaysBetweenVisits, int OneVisit, int TwoToThreeVisits, int FourToSixVisits, int SevenPlusVisits, IReadOnlyList<VisitFrequencyRowDto> Customers);
public sealed record VisitFrequencyRowDto(Guid CustomerId, string CustomerName, string Level, int Visits, decimal? AverageDaysBetweenVisits, DateTime LastVisitUtc);

public sealed record ReturningCustomersReportDto(DateTime StartUtc, DateTime EndUtc, int ActiveCustomers, int NewCustomers, int ReturningCustomers, decimal ReturningPercentage, IReadOnlyList<CustomerRetentionPeriodDto> Trend);
public sealed record CustomerRetentionPeriodDto(DateTime PeriodStartUtc, string Label, int NewCustomers, int ReturningCustomers);

public sealed record ActivityTrendsReportDto(DateTime StartUtc, DateTime EndUtc, IReadOnlyList<ActivityTrendPeriodDto> Periods);
public sealed record ActivityTrendPeriodDto(DateTime PeriodStartUtc, string Label, int ActiveCustomers, int PointsIssued, int PointsRedeemed, int Redemptions, int Purchases, decimal PurchaseAmount);

public sealed record LevelDistributionReportDto(int TotalCustomers, string? DominantLevel, decimal TopLevelPercentage, IReadOnlyList<LevelDistributionRowDto> Levels);
public sealed record LevelDistributionRowDto(string Level, int Customers, decimal Percentage, decimal AveragePoints);