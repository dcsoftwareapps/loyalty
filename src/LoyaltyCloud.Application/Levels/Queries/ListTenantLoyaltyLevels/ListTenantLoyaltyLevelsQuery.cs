using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Levels.Queries.ListTenantLoyaltyLevels;

public sealed record ListTenantLoyaltyLevelsQuery()
    : IRequest<Result<IReadOnlyList<TenantLoyaltyLevelAdminDto>>>;
