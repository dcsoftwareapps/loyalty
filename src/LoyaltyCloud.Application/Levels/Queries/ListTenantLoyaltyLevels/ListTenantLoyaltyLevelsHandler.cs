using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Levels.Queries.ListTenantLoyaltyLevels;

public sealed class ListTenantLoyaltyLevelsHandler
    : IRequestHandler<ListTenantLoyaltyLevelsQuery, Result<IReadOnlyList<TenantLoyaltyLevelAdminDto>>>
{
    private readonly ITenantLoyaltyLevelManagementService _levels;

    public ListTenantLoyaltyLevelsHandler(ITenantLoyaltyLevelManagementService levels) => _levels = levels;

    public Task<Result<IReadOnlyList<TenantLoyaltyLevelAdminDto>>> Handle(
        ListTenantLoyaltyLevelsQuery request,
        CancellationToken ct) =>
        _levels.ListAsync(ct);
}
