using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using Xunit;

namespace LoyaltyCloud.Tests.Domain;

public sealed class BillingDomainTests
{
    [Fact]
    public void Plan_uses_server_owned_period_price()
    {
        var plan = Plan();
        Assert.Equal(100m, plan.PriceFor(1)); Assert.Equal(270m, plan.PriceFor(3));
        Assert.Equal(500m, plan.PriceFor(6)); Assert.Equal(900m, plan.PriceFor(12));
        Assert.Throws<ArgumentException>(() => plan.PriceFor(2));
    }

    [Fact]
    public void Transfer_order_and_receipt_do_not_mark_order_paid()
    {
        var now = new DateTime(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc);
        var order = new BillingOrder(Guid.NewGuid(), Guid.NewGuid(), "standard", 1, 100, 16, 116, "MXN",
            BillingPaymentMethod.BankTransfer, now, now, now.AddMonths(1));
        order.AttachReceipt("private://receipt");
        Assert.Equal(BillingOrderStatus.AwaitingTransfer, order.Status);
        Assert.NotNull(order.BankReference);
    }

    [Fact]
    public void Order_can_be_paid_only_once()
    {
        var now = DateTime.UtcNow;
        var order = new BillingOrder(Guid.NewGuid(), Guid.NewGuid(), "standard", 1, 100, 16, 116, "MXN",
            BillingPaymentMethod.Card, now, now, now.AddMonths(1));
        Assert.True(order.MarkPaid(null, now));
        Assert.False(order.MarkPaid(null, now));
    }

    [Fact]
    public void Transfer_approval_is_idempotent()
    {
        var now = DateTime.UtcNow;
        var order = new BillingOrder(Guid.NewGuid(), Guid.NewGuid(), "standard", 1, 100, 16, 116, "MXN",
            BillingPaymentMethod.BankTransfer, now, now, now.AddMonths(1));

        Assert.True(order.MarkPaid("superadmin", now));
        Assert.False(order.MarkPaid("another-admin", now.AddMinutes(1)));
        Assert.Equal("superadmin", order.ApprovedBy);
        Assert.Equal(now, order.ApprovedAt);
    }

    [Fact]
    public void Billing_order_keeps_tenant_ownership()
    {
        var tenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var order = new BillingOrder(Guid.NewGuid(), tenantId, "standard", 1, 100, 16, 116, "MXN",
            BillingPaymentMethod.Card, now, now, now.AddMonths(1));

        Assert.Equal(tenantId, order.TenantId);
    }

    [Fact]
    public void Duplicate_webhook_identity_is_stable()
    {
        const string eventId = "evt_123";
        var first = new PaymentWebhookEvent(Guid.NewGuid(), PaymentProvider.Stripe, eventId, "checkout.session.completed", DateTime.UtcNow);
        var duplicate = new PaymentWebhookEvent(Guid.NewGuid(), PaymentProvider.Stripe, eventId, "checkout.session.completed", DateTime.UtcNow);

        Assert.Equal(first.Provider, duplicate.Provider);
        Assert.Equal(first.ProviderEventId, duplicate.ProviderEventId);
    }

    [Fact]
    public void Suspended_subscription_is_not_operational_but_payment_suspension_can_be_renewed()
    {
        var now = DateTime.UtcNow;
        var subscription = new TenantSubscription(Guid.NewGuid(), TenantSubscriptionStatus.Suspended, "standard",
            suspensionReason: TenantSuspensionReason.PaymentPastDue);

        Assert.False(subscription.IsOperational(now));
        Assert.Equal(now.AddMonths(1), subscription.RecordManualPayment(1, now));
        Assert.True(subscription.IsOperational(now));
    }
    [Fact]
    public void Subscription_extension_starts_from_future_paid_through_or_now()
    {
        var now = new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(now.AddMonths(3), TenantSubscription.CalculateManualPaymentPaidThrough(now.AddDays(-1), 3, now));
        Assert.Equal(now.AddMonths(1).AddDays(5), TenantSubscription.CalculateManualPaymentPaidThrough(now.AddDays(5), 1, now));
    }

    private static SubscriptionPlan Plan()
    {
        var p = new SubscriptionPlan(Guid.NewGuid(), "standard", "Standard", "MXN", DateTime.UtcNow);
        p.Update("Standard", "MXN", 100, 270, 500, 900, true, DateTime.UtcNow);
        return p;
    }
}
