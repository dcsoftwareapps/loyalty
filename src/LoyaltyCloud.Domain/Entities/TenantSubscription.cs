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

    public bool IsOperational(DateTime nowUtc) =>
        IsOperational(Status, GracePeriodEndsAt, nowUtc);

    public void Suspend()
    {
        Status = TenantSubscriptionStatus.Suspended;
    }

    public void Reactivate()
    {
        Status = TenantSubscriptionStatus.Active;
    }

    public void Cancel()
    {
        Status = TenantSubscriptionStatus.Cancelled;
    }

    public void ExtendTrial(DateTime newTrialEndUtc)
    {
        if (newTrialEndUtc.Kind == DateTimeKind.Local)
            throw new ArgumentException("La fecha de trial debe estar en UTC.", nameof(newTrialEndUtc));

        if (CurrentPeriodEnd.HasValue && newTrialEndUtc <= CurrentPeriodEnd.Value)
            throw new ArgumentException("La nueva fecha de trial debe ser posterior a la fecha actual.", nameof(newTrialEndUtc));

        if (Status == TenantSubscriptionStatus.Cancelled)
            throw new InvalidOperationException("No se puede extender el trial de un tenant cancelado.");

        CurrentPeriodEnd = DateTime.SpecifyKind(newTrialEndUtc, DateTimeKind.Utc);
    }

    public void ChangeGracePeriod(DateTime? newGracePeriodEndUtc)
    {
        if (newGracePeriodEndUtc.HasValue && newGracePeriodEndUtc.Value.Kind == DateTimeKind.Local)
            throw new ArgumentException("La fecha de gracia debe estar en UTC.", nameof(newGracePeriodEndUtc));

        GracePeriodEndsAt = newGracePeriodEndUtc.HasValue
            ? DateTime.SpecifyKind(newGracePeriodEndUtc.Value, DateTimeKind.Utc)
            : null;
    }

    public static bool IsOperational(
        TenantSubscriptionStatus status,
        DateTime? gracePeriodEndsAt,
        DateTime nowUtc) =>
        status is TenantSubscriptionStatus.Trial or TenantSubscriptionStatus.Active
        || (status == TenantSubscriptionStatus.PastDue
            && (!gracePeriodEndsAt.HasValue || gracePeriodEndsAt.Value >= nowUtc));
}
