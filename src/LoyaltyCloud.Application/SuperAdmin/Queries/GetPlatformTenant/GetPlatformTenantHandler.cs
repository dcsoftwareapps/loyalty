using LoyaltyCloud.Application.Common.Interfaces;
using MediatR;

namespace LoyaltyCloud.Application.SuperAdmin.Queries.GetPlatformTenant;

internal sealed class GetPlatformTenantHandler : IRequestHandler<GetPlatformTenantQuery, PlatformTenantDetailDto?>
{
    private readonly ISuperAdminTenantReadService _readService;

    public GetPlatformTenantHandler(ISuperAdminTenantReadService readService)
    {
        _readService = readService;
    }

    public async Task<PlatformTenantDetailDto?> Handle(GetPlatformTenantQuery request, CancellationToken cancellationToken) =>
        await _readService.GetTenantAsync(request.TenantId, cancellationToken);
}
