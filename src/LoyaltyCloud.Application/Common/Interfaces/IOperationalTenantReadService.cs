namespace LoyaltyCloud.Application.Common.Interfaces;

public interface IOperationalTenantReadService
{
    Task<IReadOnlyList<TenantExecutionInfo>> ListTenantsForExecutionAsync(CancellationToken ct = default);
}

