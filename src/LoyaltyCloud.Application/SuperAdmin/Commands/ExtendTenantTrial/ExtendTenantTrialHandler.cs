using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.SuperAdmin.Commands.ExtendTenantTrial;

internal sealed class ExtendTenantTrialHandler : IRequestHandler<ExtendTenantTrialCommand, Result>
{
    private readonly ISuperAdminTenantManagementService _management;

    public ExtendTenantTrialHandler(ISuperAdminTenantManagementService management)
    {
        _management = management;
    }

    public async Task<Result> Handle(ExtendTenantTrialCommand request, CancellationToken cancellationToken) =>
        await _management.ExtendTrialAsync(request.TenantId, request.NewTrialEndUtc, cancellationToken);
}
