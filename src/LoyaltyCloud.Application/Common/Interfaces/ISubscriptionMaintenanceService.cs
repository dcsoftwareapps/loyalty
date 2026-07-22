namespace LoyaltyCloud.Application.Common.Interfaces;

public interface ISubscriptionMaintenanceService
{
    Task<SubscriptionMaintenanceResult> ProcessAsync(CancellationToken cancellationToken = default);
}

public sealed record SubscriptionMaintenanceResult(
    int TenantsProcessed,
    int TrialsSuspended,
    int ActiveMovedToPastDue,
    int PastDueSuspended,
    int FailedTenants);
