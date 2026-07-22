using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.SuperAdmin.Commands.UpdateTenantGracePeriod;

internal sealed class UpdateTenantGracePeriodHandler : IRequestHandler<UpdateTenantGracePeriodCommand, Result>
{
    private readonly ISuperAdminTenantManagementService _management;

    public UpdateTenantGracePeriodHandler(ISuperAdminTenantManagementService management)
    {
        _management = management;
    }

    public async Task<Result> Handle(UpdateTenantGracePeriodCommand request, CancellationToken cancellationToken) =>
        await _management.UpdateGracePeriodAsync(request.TenantId, request.NewGracePeriodEndUtc, cancellationToken);
}
