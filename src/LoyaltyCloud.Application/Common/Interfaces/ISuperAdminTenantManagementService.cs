using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Application.SuperAdmin.Commands.RecordManualSubscriptionPayment;

namespace LoyaltyCloud.Application.Common.Interfaces;

public interface ISuperAdminTenantManagementService
{
    Task<Result> SuspendAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result> ReactivateAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result> CancelAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Result> ExtendTrialAsync(Guid tenantId, DateTime newTrialEndUtc, CancellationToken cancellationToken = default);
    Task<Result> UpdateGracePeriodAsync(Guid tenantId, DateTime? newGracePeriodEndUtc, CancellationToken cancellationToken = default);
    Task<Result<RecordManualSubscriptionPaymentResult>> RecordPaymentAsync(Guid tenantId, int months, CancellationToken cancellationToken = default);
}
