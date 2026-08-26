using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using Xunit;
namespace LoyaltyCloud.Tests.Domain;
public sealed class RecurringBillingDomainTests
{
 [Fact] public void Billing_profile_defaults_auto_renew_on() => Assert.True(new TenantBillingProfile(Guid.NewGuid(), Guid.NewGuid()).AutoRenewEnabled);
 [Fact] public void Toggle_off_keeps_subscription_and_sets_cancel_at_period_end()
 { var p = new TenantBillingProfile(Guid.NewGuid(), Guid.NewGuid()); p.AttachSubscription("sub_1", "active", DateTime.UtcNow.AddMonths(1), false); p.SetAutoRenew(false); Assert.False(p.AutoRenewEnabled); Assert.True(p.CancelAtPeriodEnd); Assert.Equal("sub_1", p.StripeSubscriptionId); }
 [Fact] public void Toggle_on_again_clears_cancel_at_period_end()
 { var p = new TenantBillingProfile(Guid.NewGuid(), Guid.NewGuid()); p.AttachSubscription("sub_1", "active", DateTime.UtcNow.AddMonths(1), true); p.SetAutoRenew(true); Assert.True(p.AutoRenewEnabled); Assert.False(p.CancelAtPeriodEnd); }
 [Fact] public void Subscription_deleted_preserves_operational_paid_through()
 { var through = DateTime.UtcNow.AddMonths(1); var subscription = new TenantSubscription(Guid.NewGuid(), TenantSubscriptionStatus.Active, "standard", paidThroughUtc: through); var profile = new TenantBillingProfile(Guid.NewGuid(), subscription.TenantId); profile.AttachSubscription("sub_1", "active", through, false); profile.SubscriptionDeleted("canceled", through); Assert.False(profile.AutoRenewEnabled); Assert.Equal(through, subscription.PaidThroughUtc); }
}
