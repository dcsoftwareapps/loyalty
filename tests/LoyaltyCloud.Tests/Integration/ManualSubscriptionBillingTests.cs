using LoyaltyCloud.Application;
using LoyaltyCloud.Application.Billing;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Provisioning;
using LoyaltyCloud.Application.SuperAdmin.Commands.RecordManualSubscriptionPayment;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class ManualSubscriptionBillingTests
{
    private static readonly DateTime FixedNow = new(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Provisioning_creates_trial_with_paid_through_null()
    {
        await using var env = await BillingTestEnvironment.CreateAsync();

        var result = await env.ProvisionAsync("billing-spa", "Billing Spa");

        Assert.True(result.IsSuccess, result.Error);
        var subscription = await env.PlatformReadAsync(db =>
            db.TenantSubscriptions.SingleAsync(s => s.TenantId == result.Value.TenantId));
        Assert.Equal(TenantSubscriptionStatus.Trial, subscription.Status);
        Assert.Null(subscription.PaidThroughUtc);
        Assert.Equal(FixedNow.AddDays(14), subscription.CurrentPeriodEnd);
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public void Trial_operational_depends_on_trial_end()
    {
        var current = new TenantSubscription(Guid.NewGuid(), TenantSubscriptionStatus.Trial, "trial", FixedNow, FixedNow.AddDays(1));
        var expired = new TenantSubscription(Guid.NewGuid(), TenantSubscriptionStatus.Trial, "trial", FixedNow.AddDays(-10), FixedNow.AddTicks(-1));

        Assert.True(current.IsOperational(FixedNow));
        Assert.False(expired.IsOperational(FixedNow));
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Maintenance_suspends_expired_trial()
    {
        await using var env = await BillingTestEnvironment.CreateAsync();
        var tenantId = await env.AddTenantAsync("expired-trial", TenantSubscriptionStatus.Trial, trialEnd: FixedNow.AddDays(-1));

        var result = await env.RunMaintenanceAsync();

        Assert.Equal(1, result.TrialsSuspended);
        Assert.Equal(TenantSubscriptionStatus.Suspended, await env.GetStatusAsync(tenantId));
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Trial_payment_sets_active_from_now()
    {
        await using var env = await BillingTestEnvironment.CreateAsync();
        var tenantId = await env.AddTenantAsync("trial-pay", TenantSubscriptionStatus.Trial, trialEnd: FixedNow.AddDays(10));

        var result = await env.RecordPaymentAsync(tenantId, 1);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(TenantSubscriptionStatus.Active, await env.GetStatusAsync(tenantId));
        Assert.Equal(FixedNow.AddMonths(1), result.Value.PaidThroughUtc);
    }

    [Theory]
    [Trait("Category", "ManualSubscriptionBilling")]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(12)]
    public async Task Payment_months_calculate_with_add_months(int months)
    {
        await using var env = await BillingTestEnvironment.CreateAsync();
        var tenantId = await env.AddTenantAsync(
            "months-" + months,
            TenantSubscriptionStatus.Suspended,
            suspensionReason: TenantSuspensionReason.PaymentPastDue);

        var result = await env.RecordPaymentAsync(tenantId, months);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(FixedNow.AddMonths(months), result.Value.PaidThroughUtc);
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Early_payment_extends_from_existing_paid_through()
    {
        await using var env = await BillingTestEnvironment.CreateAsync();
        var paidThrough = FixedNow.AddDays(19);
        var tenantId = await env.AddTenantAsync("early-pay", TenantSubscriptionStatus.Active, paidThrough: paidThrough);

        var result = await env.RecordPaymentAsync(tenantId, 1);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(paidThrough.AddMonths(1), result.Value.PaidThroughUtc);
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Expired_payment_extends_from_now()
    {
        await using var env = await BillingTestEnvironment.CreateAsync();
        var tenantId = await env.AddTenantAsync("expired-pay", TenantSubscriptionStatus.Active, paidThrough: FixedNow.AddDays(-1));

        var result = await env.RecordPaymentAsync(tenantId, 1);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(FixedNow.AddMonths(1), result.Value.PaidThroughUtc);
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public void Active_operational_depends_on_paid_through()
    {
        var current = new TenantSubscription(Guid.NewGuid(), TenantSubscriptionStatus.Active, "manual", paidThroughUtc: FixedNow.AddDays(1));
        var expired = new TenantSubscription(Guid.NewGuid(), TenantSubscriptionStatus.Active, "manual", paidThroughUtc: FixedNow);
        var legacy = new TenantSubscription(Guid.NewGuid(), TenantSubscriptionStatus.Active, "legacy");

        Assert.True(current.IsOperational(FixedNow));
        Assert.False(expired.IsOperational(FixedNow));
        Assert.False(legacy.IsOperational(FixedNow));
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Maintenance_moves_expired_active_to_past_due_with_grace()
    {
        await using var env = await BillingTestEnvironment.CreateAsync(graceDays: 7);
        var tenantId = await env.AddTenantAsync("active-expired", TenantSubscriptionStatus.Active, paidThrough: FixedNow.AddMinutes(-1));

        var result = await env.RunMaintenanceAsync();

        Assert.Equal(1, result.ActiveMovedToPastDue);
        var subscription = await env.GetSubscriptionAsync(tenantId);
        Assert.Equal(TenantSubscriptionStatus.PastDue, subscription.Status);
        Assert.Equal(FixedNow.AddDays(7), subscription.GracePeriodEndsAt);
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public void Past_due_operational_depends_on_grace()
    {
        var current = new TenantSubscription(Guid.NewGuid(), TenantSubscriptionStatus.PastDue, "manual", gracePeriodEndsAt: FixedNow.AddDays(1));
        var expired = new TenantSubscription(Guid.NewGuid(), TenantSubscriptionStatus.PastDue, "manual", gracePeriodEndsAt: FixedNow);

        Assert.True(current.IsOperational(FixedNow));
        Assert.False(expired.IsOperational(FixedNow));
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Maintenance_suspends_expired_grace()
    {
        await using var env = await BillingTestEnvironment.CreateAsync();
        var tenantId = await env.AddTenantAsync("grace-expired", TenantSubscriptionStatus.PastDue, graceEnd: FixedNow.AddTicks(-1));

        var result = await env.RunMaintenanceAsync();

        Assert.Equal(1, result.PastDueSuspended);
        Assert.Equal(TenantSubscriptionStatus.Suspended, await env.GetStatusAsync(tenantId));
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Past_due_payment_sets_active_and_clears_grace()
    {
        await using var env = await BillingTestEnvironment.CreateAsync();
        var tenantId = await env.AddTenantAsync("pastdue-pay", TenantSubscriptionStatus.PastDue, paidThrough: FixedNow.AddDays(-2), graceEnd: FixedNow.AddDays(3));

        var result = await env.RecordPaymentAsync(tenantId, 1);

        Assert.True(result.IsSuccess, result.Error);
        var subscription = await env.GetSubscriptionAsync(tenantId);
        Assert.Equal(TenantSubscriptionStatus.Active, subscription.Status);
        Assert.Null(subscription.GracePeriodEndsAt);
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Suspended_payment_sets_active()
    {
        await using var env = await BillingTestEnvironment.CreateAsync();
        var tenantId = await env.AddTenantAsync(
            "suspended-pay",
            TenantSubscriptionStatus.Suspended,
            suspensionReason: TenantSuspensionReason.PaymentPastDue);

        var result = await env.RecordPaymentAsync(tenantId, 1);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(TenantSubscriptionStatus.Active, await env.GetStatusAsync(tenantId));
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Save_plan_persists_active_state_and_prices()
    {
        await using var env = await BillingTestEnvironment.CreateAsync();
        var plan = new SubscriptionPlanDto(Guid.Empty, "standard", "Regular", "MXN", 250m, 700m, 1300m, 2400m, true);

        var affected = await env.SavePlanAsync(plan);
        var persisted = await env.GetPlanAsync("standard");

        Assert.Equal(1, affected);
        Assert.True(persisted.IsActive);
        Assert.Equal(250m, persisted.OneMonthPrice);
        Assert.Equal(700m, persisted.ThreeMonthPrice);
        Assert.Equal(1300m, persisted.SixMonthPrice);
        Assert.Equal(2400m, persisted.TwelveMonthPrice);
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Reload_returns_persisted_plan_values()
    {
        await using var env = await BillingTestEnvironment.CreateAsync();
        await env.SavePlanAsync(new SubscriptionPlanDto(Guid.Empty, "standard", "Regular", "MXN", 250m, 0m, 0m, 0m, false));
        var original = await env.GetPlanAsync("standard");

        await env.SavePlanAsync(original with { OneMonthPrice = 275m, ThreeMonthPrice = 750m, IsActive = true });
        var reloaded = await env.GetPlanAsync("standard");

        Assert.True(reloaded.IsActive);
        Assert.Equal(275m, reloaded.OneMonthPrice);
        Assert.Equal(750m, reloaded.ThreeMonthPrice);
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Card_order_is_persisted_and_returns_checkout_url()
    {
        var gateway = new CheckoutTestGateway();
        await using var env = await BillingTestEnvironment.CreateAsync(paymentGateway: gateway);
        var tenantId = await env.AddTenantAsync("checkout-order", TenantSubscriptionStatus.Suspended);
        await env.SavePlanAsync(new SubscriptionPlanDto(Guid.Empty, "standard", "Regular", "MXN", 250m, 0m, 0m, 0m, true, "price_test_monthly"));
        await env.EnableCardPaymentsAsync();

        var order = await env.CreateCardOrderAsync(tenantId, "standard", 1);

        Assert.Equal(BillingPaymentMethod.Card, order.PaymentMethod);
        Assert.Equal(BillingOrderStatus.Pending, order.Status);
        Assert.Equal("https://checkout.stripe.test/c/pay/cs_test_checkout", order.CheckoutUrl);
        Assert.Equal(1, gateway.CreateCalls);
        Assert.True(await env.BillingOrderExistsAsync(order.Id));
        Assert.NotNull(gateway.LastRequest);
        Assert.Contains("/billing/payment/success?token=", gateway.LastRequest!.SuccessUrl);
        Assert.DoesNotContain("orderId=", gateway.LastRequest.SuccessUrl);
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Protected_payment_result_cannot_be_read_through_another_tenant_slug()
    {
        var gateway = new CheckoutTestGateway();
        await using var env = await BillingTestEnvironment.CreateAsync(paymentGateway: gateway);
        var tenantId = await env.AddTenantAsync("result-owner", TenantSubscriptionStatus.Active,
            paidThrough: FixedNow.AddDays(10));
        await env.AddTenantAsync("result-other", TenantSubscriptionStatus.Active,
            paidThrough: FixedNow.AddDays(10));
        await env.CreatePayableCardOrderAsync(tenantId);
        var token = ExtractReturnToken(gateway.LastRequest!.SuccessUrl);

        var ownerResult = await env.GetPaymentResultAsync("result-owner", token);
        var otherResult = await env.GetPaymentResultAsync("result-other", token);

        Assert.NotNull(ownerResult);
        Assert.Equal(BillingOrderStatus.Pending, ownerResult!.Status);
        Assert.Null(otherResult);
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Stripe_webhook_reactivates_nonpayment_suspension()
    {
        var gateway = new CheckoutTestGateway();
        await using var env = await BillingTestEnvironment.CreateAsync(paymentGateway: gateway);
        var tenantId = await env.AddTenantAsync("webhook-suspended", TenantSubscriptionStatus.Suspended,
            suspensionReason: TenantSuspensionReason.PaymentPastDue);
        var order = await env.CreatePayableCardOrderAsync(tenantId);
        gateway.SetPaidConfirmation(order.Id, tenantId, "evt_reactivate");

        await env.ProcessStripeWebhookAsync();

        var subscription = await env.GetSubscriptionAsync(tenantId);
        Assert.Equal(TenantSubscriptionStatus.Active, subscription.Status);
        Assert.Equal(FixedNow.AddMonths(1), subscription.PaidThroughUtc);
        Assert.Null(subscription.SuspensionReason);
        Assert.Equal(1, await env.PaymentTransactionCountAsync(order.Id));
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Stripe_webhook_keeps_active_tenant_active()
    {
        var gateway = new CheckoutTestGateway();
        await using var env = await BillingTestEnvironment.CreateAsync(paymentGateway: gateway);
        var existingPaidThrough = FixedNow.AddDays(10);
        var tenantId = await env.AddTenantAsync("webhook-active", TenantSubscriptionStatus.Active,
            paidThrough: existingPaidThrough);
        var order = await env.CreatePayableCardOrderAsync(tenantId);
        gateway.SetPaidConfirmation(order.Id, tenantId, "evt_active");

        await env.ProcessStripeWebhookAsync();

        var subscription = await env.GetSubscriptionAsync(tenantId);
        Assert.Equal(TenantSubscriptionStatus.Active, subscription.Status);
        Assert.Equal(existingPaidThrough.AddMonths(1), subscription.PaidThroughUtc);
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Duplicate_Stripe_webhook_does_not_extend_or_create_transaction_twice()
    {
        var gateway = new CheckoutTestGateway();
        await using var env = await BillingTestEnvironment.CreateAsync(paymentGateway: gateway);
        var tenantId = await env.AddTenantAsync("webhook-duplicate", TenantSubscriptionStatus.Suspended,
            suspensionReason: TenantSuspensionReason.TrialExpired);
        var order = await env.CreatePayableCardOrderAsync(tenantId);
        gateway.SetPaidConfirmation(order.Id, tenantId, "evt_duplicate");

        await env.ProcessStripeWebhookAsync();
        var firstPaidThrough = (await env.GetSubscriptionAsync(tenantId)).PaidThroughUtc;
        await env.ProcessStripeWebhookAsync();

        Assert.Equal(firstPaidThrough, (await env.GetSubscriptionAsync(tenantId)).PaidThroughUtc);
        Assert.Equal(1, await env.PaymentTransactionCountAsync(order.Id));
        Assert.Equal(1, await env.WebhookEventCountAsync("evt_duplicate"));
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Stripe_webhook_does_not_bypass_administrative_suspension()
    {
        var gateway = new CheckoutTestGateway();
        await using var env = await BillingTestEnvironment.CreateAsync(paymentGateway: gateway);
        var tenantId = await env.AddTenantAsync("webhook-administrative", TenantSubscriptionStatus.Suspended,
            suspensionReason: TenantSuspensionReason.Administrative);
        var order = await env.CreatePayableCardOrderAsync(tenantId);
        gateway.SetPaidConfirmation(order.Id, tenantId, "evt_administrative");

        await Assert.ThrowsAsync<InvalidOperationException>(() => env.ProcessStripeWebhookAsync());

        var subscription = await env.GetSubscriptionAsync(tenantId);
        Assert.Equal(TenantSubscriptionStatus.Suspended, subscription.Status);
        Assert.Equal(TenantSuspensionReason.Administrative, subscription.SuspensionReason);
        Assert.Equal(0, await env.PaymentTransactionCountAsync(order.Id));
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Expired_Stripe_webhook_marks_pending_order_without_extending_subscription()
    {
        var gateway = new CheckoutTestGateway();
        await using var env = await BillingTestEnvironment.CreateAsync(paymentGateway: gateway);
        var tenantId = await env.AddTenantAsync("webhook-expired", TenantSubscriptionStatus.Suspended,
            suspensionReason: TenantSuspensionReason.PaymentPastDue);
        var order = await env.CreatePayableCardOrderAsync(tenantId);
        gateway.SetExpiredConfirmation(order.Id, tenantId, "evt_expired");

        await env.ProcessStripeWebhookAsync();

        Assert.Equal(BillingOrderStatus.Expired, await env.GetOrderStatusAsync(order.Id));
        Assert.Equal(0, await env.PaymentTransactionCountAsync(order.Id));
        var subscription = await env.GetSubscriptionAsync(tenantId);
        Assert.Equal(TenantSubscriptionStatus.Suspended, subscription.Status);
        Assert.Null(subscription.PaidThroughUtc);
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Paid_order_never_returns_to_expired_and_expired_webhook_is_idempotent()
    {
        var gateway = new CheckoutTestGateway();
        await using var env = await BillingTestEnvironment.CreateAsync(paymentGateway: gateway);
        var tenantId = await env.AddTenantAsync("paid-not-expired", TenantSubscriptionStatus.Suspended,
            suspensionReason: TenantSuspensionReason.TrialExpired);
        var order = await env.CreatePayableCardOrderAsync(tenantId);
        gateway.SetPaidConfirmation(order.Id, tenantId, "evt_paid_first");
        await env.ProcessStripeWebhookAsync();
        var paidThrough = (await env.GetSubscriptionAsync(tenantId)).PaidThroughUtc;

        gateway.SetExpiredConfirmation(order.Id, tenantId, "evt_expired_after_paid");
        await env.ProcessStripeWebhookAsync();
        await env.ProcessStripeWebhookAsync();

        Assert.Equal(BillingOrderStatus.Paid, await env.GetOrderStatusAsync(order.Id));
        Assert.Equal(paidThrough, (await env.GetSubscriptionAsync(tenantId)).PaidThroughUtc);
        Assert.Equal(1, await env.PaymentTransactionCountAsync(order.Id));
        Assert.Equal(1, await env.WebhookEventCountAsync("evt_expired_after_paid"));
    }

    [Theory]
    [InlineData(CheckoutSessionStatus.Expired, BillingOrderStatus.Expired)]
    [InlineData(CheckoutSessionStatus.Open, BillingOrderStatus.Pending)]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Billing_history_reconciles_only_Stripe_expired_sessions(
        CheckoutSessionStatus checkoutStatus,
        BillingOrderStatus expectedOrderStatus)
    {
        var gateway = new CheckoutTestGateway();
        await using var env = await BillingTestEnvironment.CreateAsync(paymentGateway: gateway);
        var tenantId = await env.AddTenantAsync("reconcile-" + checkoutStatus.ToString().ToLowerInvariant(),
            TenantSubscriptionStatus.Suspended, suspensionReason: TenantSuspensionReason.PaymentPastDue);
        var order = await env.CreatePayableCardOrderAsync(tenantId);
        await env.MakeOrderPotentiallyExpiredAsync(order.Id);
        gateway.CheckoutStatus = checkoutStatus;

        await env.GetTenantBillingAsync(tenantId);

        Assert.Equal(expectedOrderStatus, await env.GetOrderStatusAsync(order.Id));
        Assert.Equal(1, gateway.GetSessionCalls);
        Assert.Equal(0, await env.PaymentTransactionCountAsync(order.Id));
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Cancelled_payment_is_rejected()
    {
        await using var env = await BillingTestEnvironment.CreateAsync();
        var tenantId = await env.AddTenantAsync("cancelled-pay", TenantSubscriptionStatus.Cancelled);

        var result = await env.RecordPaymentAsync(tenantId, 1);

        Assert.True(result.IsFailure);
        Assert.Contains("cancelada", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Maintenance_is_idempotent()
    {
        await using var env = await BillingTestEnvironment.CreateAsync();
        await env.AddTenantAsync("idempotent-active", TenantSubscriptionStatus.Active, paidThrough: FixedNow.AddTicks(-1));

        var first = await env.RunMaintenanceAsync();
        var second = await env.RunMaintenanceAsync();

        Assert.Equal(1, first.ActiveMovedToPastDue);
        Assert.Equal(0, second.ActiveMovedToPastDue);
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Grace_period_zero_works()
    {
        await using var env = await BillingTestEnvironment.CreateAsync(graceDays: 0);
        var tenantId = await env.AddTenantAsync("zero-grace", TenantSubscriptionStatus.Active, paidThrough: FixedNow.AddTicks(-1));

        await env.RunMaintenanceAsync();

        var subscription = await env.GetSubscriptionAsync(tenantId);
        Assert.Equal(TenantSubscriptionStatus.PastDue, subscription.Status);
        Assert.Equal(FixedNow, subscription.GracePeriodEndsAt);
        Assert.False(subscription.IsOperational(FixedNow));
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public void Add_months_handles_end_of_month()
    {
        var subscription = new TenantSubscription(Guid.NewGuid(), TenantSubscriptionStatus.Active, "manual", paidThroughUtc: new DateTime(2026, 1, 31, 12, 0, 0, DateTimeKind.Utc));

        var paidThrough = subscription.RecordManualPayment(1, new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 2, 28, 12, 0, 0, DateTimeKind.Utc), paidThrough);
    }

    [Fact]
    [Trait("Category", "ManualSubscriptionBilling")]
    public async Task Super_admin_page_does_not_assign_status_directly()
    {
        var source = await File.ReadAllTextAsync(BillingTestEnvironment.ReadAdminPagePath("PlatformTenantDetail.razor"));

        Assert.DoesNotContain("@bind=\"tenant.Subscription.Status\"", source);
        Assert.DoesNotContain("id=\"subscription-status\"", source);
        Assert.Contains("RecordManualSubscriptionPaymentCommand", source);
    }


    [Fact]
    [Trait("Category", "RecurringBilling")]
    public async Task Upcoming_invoice_requests_notification_once_without_extending()
    {
        var gateway = new CheckoutTestGateway();
        var notifications = new RecordingBillingNotifications();
        await using var env = await BillingTestEnvironment.CreateAsync(paymentGateway: gateway, notifications: notifications);
        var paidThrough = FixedNow.AddMonths(1);
        var tenantId = await env.AddTenantAsync("upcoming-email", TenantSubscriptionStatus.Active, paidThrough: paidThrough);
        await env.ConfigureRecurringProfileAsync(tenantId, "sub_upcoming");

        gateway.SetInvoiceConfirmation("evt_upcoming", "invoice.upcoming", "sub_upcoming", "in_upcoming", paid: false);
        await env.ProcessStripeWebhookAsync();
        await env.ProcessStripeWebhookAsync();

        Assert.Equal(paidThrough, (await env.GetSubscriptionAsync(tenantId)).PaidThroughUtc);
        Assert.Single(notifications.Items);
        Assert.Equal(BillingNotificationType.UpcomingCharge, notifications.Items[0].Type);
    }

    [Fact]
    [Trait("Category", "RecurringBilling")]
    public async Task Paid_invoice_extends_once_records_transaction_and_requests_email_once()
    {
        var gateway = new CheckoutTestGateway();
        var notifications = new RecordingBillingNotifications();
        await using var env = await BillingTestEnvironment.CreateAsync(paymentGateway: gateway, notifications: notifications);
        var original = FixedNow.AddMonths(1);
        var tenantId = await env.AddTenantAsync("paid-email", TenantSubscriptionStatus.Active, paidThrough: original);
        await env.ConfigureRecurringProfileAsync(tenantId, "sub_paid");

        gateway.SetInvoiceConfirmation("evt_paid_invoice", "invoice.paid", "sub_paid", "in_paid", paid: true);
        await env.ProcessStripeWebhookAsync();
        await env.ProcessStripeWebhookAsync();

        Assert.Equal(original.AddMonths(1), (await env.GetSubscriptionAsync(tenantId)).PaidThroughUtc);
        Assert.Equal(1, await env.ExternalTransactionCountAsync("in_paid"));
        Assert.Single(notifications.Items);
        Assert.Equal(BillingNotificationType.PaymentSucceeded, notifications.Items[0].Type);
    }

    [Fact]
    [Trait("Category", "RecurringBilling")]
    public async Task Failed_invoice_keeps_vigency_applies_grace_and_requests_email_once()
    {
        var gateway = new CheckoutTestGateway();
        var notifications = new RecordingBillingNotifications();
        await using var env = await BillingTestEnvironment.CreateAsync(paymentGateway: gateway, notifications: notifications);
        var original = FixedNow.AddDays(2);
        var tenantId = await env.AddTenantAsync("failed-email", TenantSubscriptionStatus.Active, paidThrough: original);
        await env.ConfigureRecurringProfileAsync(tenantId, "sub_failed");

        gateway.SetInvoiceConfirmation("evt_failed_invoice", "invoice.payment_failed", "sub_failed", "in_failed", paid: false);
        await env.ProcessStripeWebhookAsync();
        await env.ProcessStripeWebhookAsync();

        var subscription = await env.GetSubscriptionAsync(tenantId);
        Assert.Equal(original, subscription.PaidThroughUtc);
        Assert.Equal(FixedNow.AddDays(7), subscription.GracePeriodEndsAt);
        Assert.Single(notifications.Items);
        Assert.Equal(BillingNotificationType.PaymentFailed, notifications.Items[0].Type);
    }

    [Fact]
    [Trait("Category", "RecurringBilling")]
    public async Task Notification_provider_failure_does_not_rollback_paid_invoice()
    {
        var gateway = new CheckoutTestGateway();
        var notifications = new RecordingBillingNotifications { Failure = new InvalidOperationException("email unavailable") };
        await using var env = await BillingTestEnvironment.CreateAsync(paymentGateway: gateway, notifications: notifications);
        var original = FixedNow.AddMonths(1);
        var tenantId = await env.AddTenantAsync("email-failure", TenantSubscriptionStatus.Active, paidThrough: original);
        await env.ConfigureRecurringProfileAsync(tenantId, "sub_email_failure");

        gateway.SetInvoiceConfirmation("evt_email_failure", "invoice.paid", "sub_email_failure", "in_email_failure", paid: true);
        await env.ProcessStripeWebhookAsync();

        Assert.Equal(original.AddMonths(1), (await env.GetSubscriptionAsync(tenantId)).PaidThroughUtc);
        Assert.Equal(1, await env.ExternalTransactionCountAsync("in_email_failure"));
        Assert.Equal(1, await env.WebhookEventCountAsync("evt_email_failure"));
    }

    [Fact]
    [Trait("Category", "RecurringBilling")]
    public async Task Missing_recurring_price_id_does_not_create_broken_order()
    {
        var gateway = new CheckoutTestGateway();
        await using var env = await BillingTestEnvironment.CreateAsync(paymentGateway: gateway);
        var tenantId = await env.AddTenantAsync("missing-price", TenantSubscriptionStatus.Active, paidThrough: FixedNow.AddMonths(1));
        await env.SavePlanAsync(new SubscriptionPlanDto(Guid.Empty, "standard", "Regular", "MXN", 250m, 0m, 0m, 0m, true));
        await env.EnableCardPaymentsAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => env.CreateCardOrderAsync(tenantId, "standard", 1));

        Assert.Contains("Stripe Price ID no configurado", exception.Message);
        Assert.Equal(0, await env.OrderCountAsync(tenantId));
        Assert.Equal(0, gateway.CreateCalls);
    }

    [Fact]
    [Trait("Category", "BillingEmailSettings")]
    public async Task Email_is_disabled_by_default_and_credentials_are_only_exposed_as_a_boolean()
    {
        await using var env = await BillingTestEnvironment.CreateAsync(emailPassword: "secret-placeholder");
        var settings = await env.GetEmailSettingsAsync();
        Assert.False(settings.Enabled);
        Assert.True(settings.CredentialsConfigured);
        Assert.DoesNotContain("secret-placeholder", settings.ToString());
    }

    [Fact]
    [Trait("Category", "BillingEmailSettings")]
    public async Task Email_cannot_be_enabled_without_from_address()
    {
        await using var env = await BillingTestEnvironment.CreateAsync(emailPassword: "secret-placeholder");
        await Assert.ThrowsAsync<InvalidOperationException>(() => env.SaveEmailSettingsAsync(
            new(true, "Cloudflare", null, "LoyaltyCloud", "https://admin.test", true, false)));
    }

    [Fact]
    [Trait("Category", "BillingEmailSettings")]
    public async Task Email_cannot_be_enabled_without_runtime_credentials_even_if_client_claims_they_exist()
    {
        await using var env = await BillingTestEnvironment.CreateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => env.SaveEmailSettingsAsync(
            new(true, "Cloudflare", "notifications@example.test", "LoyaltyCloud", "https://admin.test", true, false)));
    }

    [Fact]
    [Trait("Category", "BillingEmailSettings")]
    public async Task Valid_email_configuration_can_be_enabled_and_persists()
    {
        await using var env = await BillingTestEnvironment.CreateAsync(emailPassword: "secret-placeholder");
        await env.SaveEmailSettingsAsync(new(
            true, "Cloudflare", "notifications@example.test", "LoyaltyCloud",
            "https://admin.test", false, false));
        var persisted = await env.GetEmailSettingsAsync();
        Assert.True(persisted.Enabled);
        Assert.True(persisted.IsComplete);
        Assert.Equal("notifications@example.test", persisted.FromAddress);
        Assert.Equal("https://admin.test", persisted.ApplicationBaseUrl);
    }
    private sealed class BillingTestEnvironment : IAsyncDisposable
    {
        private readonly ServiceProvider _services;

        private BillingTestEnvironment(ServiceProvider services)
        {
            _services = services;
        }

        public static async Task<BillingTestEnvironment> CreateAsync(int graceDays = 7, IPaymentGateway? paymentGateway = null, IBillingNotificationService? notifications = null, string? emailPassword = null)
        {
            var dbName = "LoyaltyCloud_MT3G_" + Guid.NewGuid().ToString("N");
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True;",
                    ["Azure:KeyVaultUri"] = "",
                    ["Azure:BlobStorage:ConnectionString"] = "UseDevelopmentStorage=true",
                    ["Apple:PassTypeIdentifier"] = "pass.com.kbeautymx.loyalty",
                    ["Apple:TeamIdentifier"] = "TESTTEAM01",
                    ["Apple:WebServiceURL"] = "https://test.local",
                    ["Apple:OrganizationName"] = "LoyaltyCloud Test",
                    ["Wallet:UseRealPassSigning"] = "false",
                    ["Wallet:UseRealApns"] = "false",
                    ["Provisioning:TrialDays"] = "14",
                    ["Billing:GracePeriodDays"] = graceDays.ToString(),
                    ["Email:Password"] = emailPassword
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddApplication();
            services.AddInfrastructure(configuration, new TestHostEnvironment());
            if (paymentGateway is not null)
            {
                services.RemoveAll<IPaymentGateway>();
                services.AddSingleton(paymentGateway);
            }
            if (notifications is not null)
            {
                services.RemoveAll<IBillingNotificationService>();
                services.AddSingleton(notifications);
            }
            services.RemoveAll<IDateTimeProvider>();
            services.AddSingleton<IDateTimeProvider>(new FixedClock(FixedNow));

            var provider = services.BuildServiceProvider(validateScopes: true);
            var env = new BillingTestEnvironment(provider);
            await env.InitializeAsync();
            return env;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                using var scope = _services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
            }
            finally
            {
                await _services.DisposeAsync();
            }
        }

        public async Task<LoyaltyCloud.Common.Results.Result<ProvisionTenantResult>> ProvisionAsync(string slug, string displayName)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ProvisionTenantCommand(
                slug,
                displayName,
                TimeZoneId: null,
                AdminUsername: "owner",
                AdminPassword: "Tenant123!",
                PrimaryColor: null,
                SecondaryColor: null,
                SupportPhone: null,
                WhatsAppUrl: null,
                InstagramUrl: null,
                TermsUrl: null));
        }

        public async Task<LoyaltyCloud.Common.Results.Result<RecordManualSubscriptionPaymentResult>> RecordPaymentAsync(Guid tenantId, int months)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RecordManualSubscriptionPaymentCommand(tenantId, months));
        }

        public async Task<SubscriptionMaintenanceResult> RunMaintenanceAsync()
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ISubscriptionMaintenanceService>().ProcessAsync();
        }

        public async Task<BillingEmailSettingsDto> GetEmailSettingsAsync()
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IBillingService>().GetEmailSettingsAsync();
        }

        public async Task SaveEmailSettingsAsync(BillingEmailSettingsDto settings)
        {
            using var scope = _services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IBillingService>().SaveEmailSettingsAsync(settings);
        }
        public async Task<int> SavePlanAsync(SubscriptionPlanDto plan)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IBillingService>().SavePlanAsync(plan);
        }

        public async Task<SubscriptionPlanDto> GetPlanAsync(string code)
        {
            using var scope = _services.CreateScope();
            var plans = await scope.ServiceProvider.GetRequiredService<IBillingService>().GetPlansAsync();
            return plans.Single(x => x.Code == code);
        }

        public async Task EnableCardPaymentsAsync()
        {
            using var scope = _services.CreateScope();
            var billing = scope.ServiceProvider.GetRequiredService<IBillingService>();
            var settings = await billing.GetSettingsAsync();
            await billing.SaveSettingsAsync(settings with { CardPaymentsEnabled = true });
        }

        public async Task<BillingOrderDto> CreateCardOrderAsync(Guid tenantId, string planCode, int months)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IBillingService>().CreateOrderAsync(
                tenantId, planCode, months, BillingPaymentMethod.Card, "https://admin.test");
        }

        public async Task<BillingOrderDto> CreatePayableCardOrderAsync(Guid tenantId)
        {
            await SavePlanAsync(new SubscriptionPlanDto(
                Guid.Empty, "standard", "Regular", "MXN", 250m, 0m, 0m, 0m, true, "price_test_monthly"));
            await EnableCardPaymentsAsync();
            return await CreateCardOrderAsync(tenantId, "standard", 1);
        }

        public async Task ProcessStripeWebhookAsync()
        {
            using var scope = _services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IBillingService>()
                .ProcessStripeWebhookAsync("test-payload", "test-signature");
        }

        public async Task<TenantBillingDto> GetTenantBillingAsync(Guid tenantId)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IBillingService>()
                .GetTenantBillingAsync(tenantId);
        }

        public async Task<BillingPaymentResultDto?> GetPaymentResultAsync(string tenantSlug, string token)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IBillingService>()
                .GetPaymentResultAsync(tenantSlug, token);
        }

        public async Task MakeOrderPotentiallyExpiredAsync(Guid orderId)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var order = await db.BillingOrders.IgnoreQueryFilters().SingleAsync(x => x.Id == orderId);
            var tenantSlug = await db.Tenants.Where(x => x.Id == order.TenantId).Select(x => x.Slug).SingleAsync();
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(order.TenantId, tenantSlug);
            db.Entry(order).Property(nameof(BillingOrder.ExpiresAt)).CurrentValue = FixedNow.AddTicks(-1);
            await db.SaveChangesAsync();
        }

        public async Task<BillingOrderStatus> GetOrderStatusAsync(Guid orderId)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AppDbContext>().BillingOrders
                .IgnoreQueryFilters().Where(x => x.Id == orderId).Select(x => x.Status).SingleAsync();
        }

        public async Task<int> PaymentTransactionCountAsync(Guid orderId)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AppDbContext>().PaymentTransactions
                .IgnoreQueryFilters().CountAsync(x => x.BillingOrderId == orderId);
        }

        public async Task<int> WebhookEventCountAsync(string eventId)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AppDbContext>().PaymentWebhookEvents
                .CountAsync(x => x.ProviderEventId == eventId);
        }

        public async Task<bool> BillingOrderExistsAsync(Guid orderId)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AppDbContext>().BillingOrders
                .IgnoreQueryFilters().AnyAsync(x => x.Id == orderId);
        }

        public async Task<Guid> AddTenantAsync(
            string slug,
            TenantSubscriptionStatus status,
            DateTime? trialEnd = null,
            DateTime? paidThrough = null,
            DateTime? graceEnd = null,
            TenantSuspensionReason? suspensionReason = null)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenantId = Guid.NewGuid();
            db.Tenants.Add(new Tenant(tenantId, slug, slug.Replace("-", " "), "America/Tijuana", FixedNow));
            db.TenantBrandings.Add(new TenantBranding(tenantId));
            db.TenantSubscriptions.Add(new TenantSubscription(
                tenantId,
                status,
                "manual",
                currentPeriodStart: FixedNow.AddDays(-14),
                currentPeriodEnd: trialEnd,
                paidThroughUtc: paidThrough,
                gracePeriodEndsAt: graceEnd,
                suspensionReason: suspensionReason));
            await db.SaveChangesAsync();
            return tenantId;
        }


        public async Task ConfigureRecurringProfileAsync(Guid tenantId, string subscriptionId)
        {
            await GetTenantBillingAsync(tenantId);
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var slug = await db.Tenants.IgnoreQueryFilters().Where(x => x.Id == tenantId).Select(x => x.Slug).SingleAsync();
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(tenantId, slug);
            var profile = await db.TenantBillingProfiles.IgnoreQueryFilters().SingleAsync(x => x.TenantId == tenantId);
            profile.SetContactEmail("billing@example.test");
            profile.AttachSubscription(subscriptionId, "active", FixedNow.AddMonths(1), false, 1, 290m, "MXN", "Visa", "4242");
            await db.SaveChangesAsync();
        }

        public async Task<int> ExternalTransactionCountAsync(string externalId)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AppDbContext>().PaymentTransactions
                .IgnoreQueryFilters().CountAsync(x => x.ExternalTransactionId == externalId);
        }

        public async Task<int> OrderCountAsync(Guid tenantId)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AppDbContext>().BillingOrders
                .IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId);
        }

        public async Task<TenantSubscription> GetSubscriptionAsync(Guid tenantId)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AppDbContext>()
                .TenantSubscriptions
                .AsNoTracking()
                .SingleAsync(s => s.TenantId == tenantId);
        }

        public async Task<TenantSubscriptionStatus> GetStatusAsync(Guid tenantId)
        {
            using var scope = _services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<AppDbContext>()
                .TenantSubscriptions
                .Where(s => s.TenantId == tenantId)
                .Select(s => s.Status)
                .SingleAsync();
        }

        public async Task<T> PlatformReadAsync<T>(Func<AppDbContext, Task<T>> query)
        {
            using var scope = _services.CreateScope();
            return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }

        public static string ReadAdminPagePath(string fileName) =>
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "src",
                "LoyaltyCloud.Admin",
                "Pages",
                fileName);

        private async Task InitializeAsync()
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureDeletedAsync();
            await db.Database.MigrateAsync();
        }
    }

    private sealed class FixedClock : IDateTimeProvider
    {
        public FixedClock(DateTime utcNow)
        {
            UtcNow = utcNow;
            Today = utcNow.Date;
        }

        public DateTime UtcNow { get; }
        public DateTime Today { get; }
    }


    private sealed class RecordingBillingNotifications : IBillingNotificationService
    {
        public List<BillingNotification> Items { get; } = [];
        public Exception? Failure { get; init; }
        public Task SendAsync(BillingNotification notification, CancellationToken ct = default)
        {
            Items.Add(notification);
            if (Failure is not null) throw Failure;
            return Task.CompletedTask;
        }
    }

    private sealed class CheckoutTestGateway : IPaymentGateway
    {
        private StripePaymentConfirmation? _confirmation;
        public bool IsAvailable => true;
        public int CreateCalls { get; private set; }
        public CheckoutGatewayRequest? LastRequest { get; private set; }
        public int GetSessionCalls { get; private set; }
        public CheckoutSessionStatus CheckoutStatus { get; set; } = CheckoutSessionStatus.Open;

        public Task<CheckoutGatewayResult> CreateCheckoutAsync(CheckoutGatewayRequest request, CancellationToken ct = default)
        {
            CreateCalls++;
            LastRequest = request;
            return Task.FromResult(new CheckoutGatewayResult(
                "cs_test_checkout",
                "https://checkout.stripe.test/c/pay/cs_test_checkout"));
        }

        public void SetPaidConfirmation(Guid orderId, Guid tenantId, string eventId) =>
            _confirmation = new StripePaymentConfirmation(
                eventId, "checkout.session.completed", "cs_test_checkout", "pi_test_payment",
                orderId, tenantId, 29000, "mxn", true, null, null);

        public void SetExpiredConfirmation(Guid orderId, Guid tenantId, string eventId) =>
            _confirmation = new StripePaymentConfirmation(
                eventId, "checkout.session.expired", "cs_test_checkout", string.Empty,
                orderId, tenantId, 29000, "mxn", false, null, null);

        public void SetInvoiceConfirmation(string eventId, string eventType, string subscriptionId, string invoiceId, bool paid)
            => _confirmation = new StripePaymentConfirmation(eventId, eventType, invoiceId, invoiceId, Guid.Empty, Guid.Empty, 29000, "mxn", paid, "Visa", "4242", SubscriptionId: subscriptionId, InvoiceId: invoiceId, PeriodEndUtc: FixedNow.AddMonths(2));

        public Task<CheckoutSessionSnapshot> GetCheckoutSessionAsync(string sessionId, CancellationToken ct = default)
        {
            GetSessionCalls++;
            return Task.FromResult(new CheckoutSessionSnapshot(CheckoutStatus, "unpaid"));
        }

        public StripePaymentConfirmation ParseWebhook(string payload, string signature) =>
            _confirmation ?? throw new InvalidOperationException("Test confirmation not configured.");
    }

    private static string ExtractReturnToken(string url)
    {
        var query = new Uri(url).Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        var value = query.Single(x => x.StartsWith("token=", StringComparison.Ordinal)).Split('=', 2)[1];
        return Uri.UnescapeDataString(value);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "LoyaltyCloud.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
