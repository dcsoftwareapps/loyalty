namespace LoyaltyCloud.Application.Common.Interfaces;

public interface IDefaultTenantResolutionService
{
    Task ResolveDefaultTenantIfMissingAsync(CancellationToken ct = default);
}
