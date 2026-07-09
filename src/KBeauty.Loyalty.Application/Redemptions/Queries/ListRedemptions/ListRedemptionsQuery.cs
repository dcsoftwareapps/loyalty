using KBeauty.Loyalty.Common.Results;
using KBeauty.Loyalty.Domain.Enums;
using MediatR;

namespace KBeauty.Loyalty.Application.Redemptions.Queries.ListRedemptions;

public sealed record ListRedemptionsQuery(
    RedemptionStatus? Status = null,
    string? Search = null) : IRequest<Result<IReadOnlyList<RedemptionHistoryItemDto>>>;
