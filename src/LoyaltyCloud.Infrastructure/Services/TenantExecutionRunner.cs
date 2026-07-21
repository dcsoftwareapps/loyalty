using LoyaltyCloud.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class TenantExecutionRunner : ITenantExecutionRunner
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TenantExecutionRunner> _logger;

    public TenantExecutionRunner(
        IServiceScopeFactory scopeFactory,
        ILogger<TenantExecutionRunner> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<TenantExecutionSummary> RunForOperationalTenantsAsync(
        string jobName,
        Func<IServiceProvider, TenantExecutionInfo, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        using var platformScope = _scopeFactory.CreateScope();
        var tenantReadService = platformScope.ServiceProvider.GetRequiredService<IOperationalTenantReadService>();
        var tenants = await tenantReadService.ListTenantsForExecutionAsync(cancellationToken);

        var eligible = 0;
        var succeeded = 0;
        var failed = 0;
        var skipped = 0;

        foreach (var tenant in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!tenant.IsOperational)
            {
                skipped++;
                _logger.LogInformation(
                    "Skipping tenant job. TenantId={TenantId}, TenantSlug={TenantSlug}, Job={Job}, Reason={Reason}",
                    tenant.TenantId,
                    tenant.Slug,
                    jobName,
                    tenant.SkipReason);
                continue;
            }

            eligible++;

            try
            {
                using var tenantScope = _scopeFactory.CreateScope();
                var tenantContext = tenantScope.ServiceProvider.GetRequiredService<IMutableTenantContext>();
                tenantContext.SetTenant(tenant.TenantId, tenant.Slug);

                _logger.LogInformation(
                    "Starting tenant job. TenantId={TenantId}, TenantSlug={TenantSlug}, Job={Job}",
                    tenant.TenantId,
                    tenant.Slug,
                    jobName);

                await operation(tenantScope.ServiceProvider, tenant, cancellationToken);
                succeeded++;

                _logger.LogInformation(
                    "Completed tenant job. TenantId={TenantId}, TenantSlug={TenantSlug}, Job={Job}",
                    tenant.TenantId,
                    tenant.Slug,
                    jobName);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(
                    ex,
                    "Tenant job failed. TenantId={TenantId}, TenantSlug={TenantSlug}, Job={Job}",
                    tenant.TenantId,
                    tenant.Slug,
                    jobName);
            }
        }

        _logger.LogInformation(
            "Tenant job cycle finished. Job={Job}, EligibleTenantCount={EligibleTenantCount}, SucceededTenantCount={SucceededTenantCount}, FailedTenantCount={FailedTenantCount}, SkippedTenantCount={SkippedTenantCount}",
            jobName,
            eligible,
            succeeded,
            failed,
            skipped);

        return new TenantExecutionSummary(eligible, succeeded, failed, skipped);
    }
}

