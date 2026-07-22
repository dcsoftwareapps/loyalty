using LoyaltyCloud.Application.Common.Interfaces;
using MediatR;

namespace LoyaltyCloud.Application.SuperAdmin.Queries.ListPlatformTenants;

internal sealed class ListPlatformTenantsHandler
    : IRequestHandler<ListPlatformTenantsQuery, IReadOnlyList<PlatformTenantListItemDto>>
{
    private readonly ISuperAdminTenantReadService _readService;

    public ListPlatformTenantsHandler(ISuperAdminTenantReadService readService)
    {
        _readService = readService;
    }

    public async Task<IReadOnlyList<PlatformTenantListItemDto>> Handle(
        ListPlatformTenantsQuery request,
        CancellationToken cancellationToken) =>
        await _readService.ListTenantsAsync(cancellationToken);
}
