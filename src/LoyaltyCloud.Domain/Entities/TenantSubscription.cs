using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Domain.Entities;

public sealed class TenantSubscription
{
    public Guid TenantId { get; private set; }
    public TenantSubscriptionStatus Status { get; private set; }
    public string PlanCode { get; private set; } = string.Empty;
    public DateTime? CurrentPeriodStart { get; private set; }
    public DateTime? CurrentPeriodEnd { get; private set; }
    public DateTime? GracePeriodEndsAt { get; private set; }
    public DateTime? LastPaymentAt { get; private set; }

    public Tenant? Tenant { get; private set; }

    private TenantSubscription() { }

    public TenantSubscription(
        Guid tenantId,
        TenantSubscriptionStatus status,
        string planCode,
        DateTime? currentPeriodStart = null,
        DateTime? currentPeriodEnd = null,
        DateTime? gracePeriodEndsAt = null,
        DateTime? lastPaymentAt = null)
    {
        TenantId = tenantId == Guid.Empty
            ? throw new ArgumentException("TenantId requerido.", nameof(tenantId))
            : tenantId;
        Status = status;
        PlanCode = Tenant.Require(planCode, nameof(planCode), 50);
        CurrentPeriodStart = currentPeriodStart;
        CurrentPeriodEnd = currentPeriodEnd;
        GracePeriodEndsAt = gracePeriodEndsAt;
        LastPaymentAt = lastPaymentAt;
    }
}
