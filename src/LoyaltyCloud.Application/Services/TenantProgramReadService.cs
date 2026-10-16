using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.Repositories;

namespace LoyaltyCloud.Application.Services;

internal sealed class TenantProgramReadService(
    ITenantContext context,
    ITenantRepository tenants) : ITenantProgramReadService
{
    public async Task<LoyaltyProgramType> GetCurrentAsync(CancellationToken ct = default)
    {
        var tenant = await tenants.GetByIdAsync(context.RequireTenantId(), ct)
            ?? throw new InvalidOperationException("El tenant actual no existe.");
        return tenant.ProgramType;
    }
}
