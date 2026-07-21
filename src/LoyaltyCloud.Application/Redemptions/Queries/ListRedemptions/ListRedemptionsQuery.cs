using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Domain.Enums;
using MediatR;

namespace LoyaltyCloud.Application.Redemptions.Queries.ListRedemptions;

public sealed record ListRedemptionsQuery(
    RedemptionStatus? Status = null,
    string? Search = null) : IRequest<Result<IReadOnlyList<RedemptionHistoryItemDto>>>;
