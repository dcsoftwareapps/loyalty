using LoyaltyCloud.Domain.Common;
namespace LoyaltyCloud.Domain.Entities;
public sealed class TenantBillingProfile : Entity, ITenantOwned
{
 public Guid TenantId { get; private set; } public bool AutoRenewEnabled { get; private set; } = true;
 public string? BillingContactEmail { get; private set; } public string? StripeCustomerId { get; private set; }
 public string? StripeSubscriptionId { get; private set; } public string? StripeSubscriptionStatus { get; private set; }
 public DateTime? StripeCurrentPeriodEndUtc { get; private set; } public bool CancelAtPeriodEnd { get; private set; }
 public int? RecurringMonths { get; private set; } public decimal? RecurringAmount { get; private set; } public string? RecurringCurrency { get; private set; }
 public string? CardBrand { get; private set; } public string? CardLast4 { get; private set; }
 private TenantBillingProfile() { }
 public TenantBillingProfile(Guid id, Guid tenantId) : base(id) { if (tenantId == Guid.Empty) throw new ArgumentException("TenantId requerido.", nameof(tenantId)); TenantId = tenantId; }
 public void SetContactEmail(string? email) => BillingContactEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
 public void SetAutoRenew(bool enabled) { AutoRenewEnabled = enabled; CancelAtPeriodEnd = !enabled && StripeSubscriptionId is not null; }
 public void AttachCustomer(string customerId) => StripeCustomerId = Require(customerId, nameof(customerId));
 public void AttachSubscription(string subscriptionId, string? status, DateTime? periodEndUtc, bool cancelAtPeriodEnd, int? months = null, decimal? amount = null, string? currency = null, string? brand = null, string? last4 = null)
 { StripeSubscriptionId = Require(subscriptionId, nameof(subscriptionId)); SyncSubscription(status, periodEndUtc, cancelAtPeriodEnd); RecurringMonths = months ?? RecurringMonths; RecurringAmount = amount ?? RecurringAmount; RecurringCurrency = currency?.Trim().ToUpperInvariant() ?? RecurringCurrency; CardBrand = brand ?? CardBrand; CardLast4 = last4 ?? CardLast4; }
 public void SyncSubscription(string? status, DateTime? periodEndUtc, bool cancelAtPeriodEnd)
 { StripeSubscriptionStatus = status; StripeCurrentPeriodEndUtc = periodEndUtc; CancelAtPeriodEnd = cancelAtPeriodEnd; AutoRenewEnabled = !cancelAtPeriodEnd && status != "canceled"; }
 public void SubscriptionDeleted(string? status, DateTime? periodEndUtc)
 { StripeSubscriptionStatus = status ?? "canceled"; StripeCurrentPeriodEndUtc = periodEndUtc ?? StripeCurrentPeriodEndUtc; CancelAtPeriodEnd = true; AutoRenewEnabled = false; }
 private static string Require(string value, string name) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Valor requerido.", name) : value.Trim();
}
