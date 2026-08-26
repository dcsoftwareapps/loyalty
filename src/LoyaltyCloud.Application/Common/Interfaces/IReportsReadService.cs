using LoyaltyCloud.Application.Admin.Queries.GetReportsSummary;

namespace LoyaltyCloud.Application.Common.Interfaces;

public interface IReportsReadService
{
    Task<ReportsSummaryDto> GetReportsSummaryAsync(GetReportsSummaryQuery query, CancellationToken ct = default);
    Task<ReportsInactiveCustomersDto> GetInactiveCustomersAsync(GetInactiveCustomersReportQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<ReportsTopRewardDto>> GetTopRewardsAsync(GetTopRewardsReportQuery query, CancellationToken ct = default);
}
