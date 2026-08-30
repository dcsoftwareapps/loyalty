using LoyaltyCloud.Application.Admin.Queries.AdvancedReports;
using LoyaltyCloud.Application.Admin.Queries.GetReportsSummary;

namespace LoyaltyCloud.Application.Common.Interfaces;

public interface IReportsReadService
{
    Task<ReportsSummaryDto> GetReportsSummaryAsync(GetReportsSummaryQuery query, CancellationToken ct = default);
    Task<ReportsInactiveCustomersDto> GetInactiveCustomersAsync(GetInactiveCustomersReportQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<ReportsTopRewardDto>> GetTopRewardsAsync(GetTopRewardsReportQuery query, CancellationToken ct = default);
    Task<TopCustomersReportDto> GetTopCustomersAsync(GetTopCustomersReportQuery query, CancellationToken ct = default);
    Task<VisitFrequencyReportDto> GetVisitFrequencyAsync(GetVisitFrequencyReportQuery query, CancellationToken ct = default);
    Task<ReturningCustomersReportDto> GetReturningCustomersAsync(GetReturningCustomersReportQuery query, CancellationToken ct = default);
    Task<ActivityTrendsReportDto> GetActivityTrendsAsync(GetActivityTrendsReportQuery query, CancellationToken ct = default);
    Task<LevelDistributionReportDto> GetLevelDistributionAsync(GetLevelDistributionReportQuery query, CancellationToken ct = default);
}