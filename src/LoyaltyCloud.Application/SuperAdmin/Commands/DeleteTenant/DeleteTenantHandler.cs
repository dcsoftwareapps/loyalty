using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.SuperAdmin.Commands.DeleteTenant;

internal sealed class DeleteTenantHandler : IRequestHandler<DeleteTenantCommand, Result>
{
    private readonly ISuperAdminTenantManagementService _management;

    public DeleteTenantHandler(ISuperAdminTenantManagementService management)
    {
        _management = management;
    }

    public async Task<Result> Handle(DeleteTenantCommand request, CancellationToken cancellationToken) =>
        await _management.DeleteAsync(request.TenantId, request.ConfirmationSlug, cancellationToken);
}
