using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.SuperAdmin.Commands.CancelTenant;

internal sealed class CancelTenantHandler : IRequestHandler<CancelTenantCommand, Result>
{
    private readonly ISuperAdminTenantManagementService _management;

    public CancelTenantHandler(ISuperAdminTenantManagementService management)
    {
        _management = management;
    }

    public async Task<Result> Handle(CancelTenantCommand request, CancellationToken cancellationToken) =>
        await _management.CancelAsync(request.TenantId, cancellationToken);
}
