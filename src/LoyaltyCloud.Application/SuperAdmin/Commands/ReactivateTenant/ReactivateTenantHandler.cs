using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.SuperAdmin.Commands.ReactivateTenant;

internal sealed class ReactivateTenantHandler : IRequestHandler<ReactivateTenantCommand, Result>
{
    private readonly ISuperAdminTenantManagementService _management;

    public ReactivateTenantHandler(ISuperAdminTenantManagementService management)
    {
        _management = management;
    }

    public async Task<Result> Handle(ReactivateTenantCommand request, CancellationToken cancellationToken) =>
        await _management.ReactivateAsync(request.TenantId, cancellationToken);
}
