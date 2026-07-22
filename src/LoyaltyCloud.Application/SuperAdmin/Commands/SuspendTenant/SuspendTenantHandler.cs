using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.SuperAdmin.Commands.SuspendTenant;

internal sealed class SuspendTenantHandler : IRequestHandler<SuspendTenantCommand, Result>
{
    private readonly ISuperAdminTenantManagementService _management;

    public SuspendTenantHandler(ISuperAdminTenantManagementService management)
    {
        _management = management;
    }

    public async Task<Result> Handle(SuspendTenantCommand request, CancellationToken cancellationToken) =>
        await _management.SuspendAsync(request.TenantId, cancellationToken);
}
