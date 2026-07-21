namespace LoyaltyCloud.Application.Common.Interfaces;

public interface ITenantExecutionRunner
{
    Task<TenantExecutionSummary> RunForOperationalTenantsAsync(
        string jobName,
        Func<IServiceProvider, TenantExecutionInfo, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);
}

