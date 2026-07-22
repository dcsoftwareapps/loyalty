using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Domain.Entities;

public sealed class TenantSubscription
{
    public Guid TenantId { get; private set; }
    public TenantSubscriptionStatus Status { get; private set; }
    public string PlanCode { get; private set; } = string.Empty;
    public DateTime? CurrentPeriodStart { get; private set; }
    public DateTime? CurrentPeriodEnd { get; private set; }
    public DateTime? PaidThroughUtc { get; private set; }
    public DateTime? GracePeriodEndsAt { get; private set; }
    public DateTime? LastPaymentAt { get; private set; }
    public TenantSuspensionReason? SuspensionReason { get; private set; }

    public Tenant? Tenant { get; private set; }

    private TenantSubscription() { }

    public TenantSubscription(
        Guid tenantId,
        TenantSubscriptionStatus status,
        string planCode,
        DateTime? currentPeriodStart = null,
        DateTime? currentPeriodEnd = null,
        DateTime? paidThroughUtc = null,
        DateTime? gracePeriodEndsAt = null,
        DateTime? lastPaymentAt = null,
        TenantSuspensionReason? suspensionReason = null)
    {
        TenantId = tenantId == Guid.Empty
            ? throw new ArgumentException("TenantId requerido.", nameof(tenantId))
            : tenantId;
        Status = status;
        PlanCode = Tenant.Require(planCode, nameof(planCode), 50);
        CurrentPeriodStart = currentPeriodStart;
        CurrentPeriodEnd = currentPeriodEnd;
        PaidThroughUtc = paidThroughUtc;
        GracePeriodEndsAt = gracePeriodEndsAt;
        LastPaymentAt = lastPaymentAt;
        SuspensionReason = status == TenantSubscriptionStatus.Suspended ? suspensionReason : null;
    }

    public bool IsOperational(DateTime nowUtc) =>
        IsOperational(Status, CurrentPeriodEnd, PaidThroughUtc, GracePeriodEndsAt, nowUtc);

    public void SuspendAdministratively()
    {
        Status = TenantSubscriptionStatus.Suspended;
        SuspensionReason = TenantSuspensionReason.Administrative;
    }

    public void Reactivate(DateTime nowUtc)
    {
        var normalizedNow = NormalizeUtc(nowUtc);
        if (Status != TenantSubscriptionStatus.Suspended || SuspensionReason != TenantSuspensionReason.Administrative)
            throw new InvalidOperationException("Solo se puede reactivar una suspension administrativa.");
        if (PaidThroughUtc is null || PaidThroughUtc <= normalizedNow)
            throw new InvalidOperationException("No se puede reactivar sin una vigencia pagada vigente.");

        Status = TenantSubscriptionStatus.Active;
        SuspensionReason = null;
    }

    public void Cancel()
    {
        Status = TenantSubscriptionStatus.Cancelled;
        SuspensionReason = null;
    }

    public void ExtendTrial(DateTime newTrialEndUtc)
    {
        if (Status != TenantSubscriptionStatus.Trial)
            throw new InvalidOperationException("Solo se puede extender el trial de una suscripcion en Trial.");

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

    public DateTime RecordManualPayment(int months, DateTime nowUtc)
    {
        if (months is not (1 or 3 or 6 or 12))
            throw new ArgumentException("Meses permitidos: 1, 3, 6 o 12.", nameof(months));
        if (Status == TenantSubscriptionStatus.Cancelled)
            throw new InvalidOperationException("Una suscripcion cancelada no puede reactivarse mediante un pago.");
        if (Status == TenantSubscriptionStatus.Suspended
            && SuspensionReason is TenantSuspensionReason.Administrative or null)
        {
            throw new InvalidOperationException(
                SuspensionReason == TenantSuspensionReason.Administrative
                    ? "El tenant esta suspendido administrativamente y debe reactivarse antes de registrar el pago."
                    : "La suspension legacy no tiene motivo definido. Requiere decision administrativa explicita antes de registrar un pago.");
        }

        var normalizedNow = NormalizeUtc(nowUtc);
        var paidThrough = PreviewManualPayment(months, normalizedNow);

        Status = TenantSubscriptionStatus.Active;
        SuspensionReason = null;
        PaidThroughUtc = paidThrough;
        GracePeriodEndsAt = null;
        LastPaymentAt = normalizedNow;

        return paidThrough;
    }

    public DateTime PreviewManualPayment(int months, DateTime nowUtc) =>
        CalculateManualPaymentPaidThrough(PaidThroughUtc, months, nowUtc);

    public static DateTime CalculateManualPaymentPaidThrough(DateTime? paidThroughUtc, int months, DateTime nowUtc)
    {
        if (months is not (1 or 3 or 6 or 12))
            throw new ArgumentException("Meses permitidos: 1, 3, 6 o 12.", nameof(months));

        var normalizedNow = NormalizeUtc(nowUtc);
        var startsFrom = paidThroughUtc.HasValue && paidThroughUtc.Value > normalizedNow
            ? paidThroughUtc.Value
            : normalizedNow;
        return startsFrom.AddMonths(months);
    }

    public bool ExpireIfNeeded(DateTime nowUtc, int gracePeriodDays)
    {
        var normalizedNow = NormalizeUtc(nowUtc);
        var graceEndsAt = normalizedNow.AddDays(Math.Max(0, gracePeriodDays));

        if (Status == TenantSubscriptionStatus.Trial
            && CurrentPeriodEnd.HasValue
            && CurrentPeriodEnd.Value <= normalizedNow)
        {
            Status = TenantSubscriptionStatus.Suspended;
            SuspensionReason = TenantSuspensionReason.TrialExpired;
            return true;
        }

        if (Status == TenantSubscriptionStatus.Active
            && PaidThroughUtc.HasValue
            && PaidThroughUtc.Value <= normalizedNow)
        {
            Status = TenantSubscriptionStatus.PastDue;
            GracePeriodEndsAt = graceEndsAt;
            SuspensionReason = null;
            return true;
        }

        if (Status == TenantSubscriptionStatus.PastDue
            && GracePeriodEndsAt.HasValue
            && GracePeriodEndsAt.Value <= normalizedNow)
        {
            Status = TenantSubscriptionStatus.Suspended;
            SuspensionReason = TenantSuspensionReason.PaymentPastDue;
            return true;
        }

        return false;
    }

    public static bool IsOperational(
        TenantSubscriptionStatus status,
        DateTime? currentPeriodEnd,
        DateTime? paidThroughUtc,
        DateTime? gracePeriodEndsAt,
        DateTime nowUtc)
    {
        var normalizedNow = NormalizeUtc(nowUtc);
        return status switch
        {
            TenantSubscriptionStatus.Trial => currentPeriodEnd.HasValue && currentPeriodEnd.Value > normalizedNow,
            TenantSubscriptionStatus.Active => paidThroughUtc.HasValue && paidThroughUtc.Value > normalizedNow,
            TenantSubscriptionStatus.PastDue => gracePeriodEndsAt.HasValue && gracePeriodEndsAt.Value > normalizedNow,
            _ => false
        };
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value.ToUniversalTime()
        };
}
