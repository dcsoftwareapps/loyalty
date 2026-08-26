using LoyaltyCloud.Domain.Common;
using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Domain.Entities;

public sealed class BillingOrder : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }
    public string PlanCode { get; private set; } = string.Empty;
    public int Months { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal Tax { get; private set; }
    public decimal Total { get; private set; }
    public string Currency { get; private set; } = "MXN";
    public BillingOrderStatus Status { get; private set; }
    public BillingPaymentMethod PaymentMethod { get; private set; }
    public PaymentProvider Provider { get; private set; } public BillingPaymentKind PaymentKind { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public string? ExternalCheckoutId { get; private set; }
    public string? BankReference { get; private set; }
    public string? ReceiptUrl { get; private set; }
    public DateTime SubscriptionFromUtc { get; private set; }
    public DateTime SubscriptionThroughUtc { get; private set; }
    public string? ApprovedBy { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    private BillingOrder() { }
    public BillingOrder(Guid id, Guid tenantId, string planCode, int months, decimal subtotal, decimal tax, decimal total,
        string currency, BillingPaymentMethod method, DateTime nowUtc, DateTime fromUtc, DateTime throughUtc, BillingPaymentKind? paymentKind = null) : base(id)
    {
        if (tenantId == Guid.Empty || months is not (1 or 3 or 6 or 12) || subtotal < 0 || tax < 0 || total < 0) throw new ArgumentException("Orden inválida.");
        TenantId = tenantId; PlanCode = planCode; Months = months; Subtotal = subtotal; Tax = tax; Total = total; Currency = currency;
        PaymentMethod = method; Provider = method == BillingPaymentMethod.Card ? PaymentProvider.Stripe : PaymentProvider.Manual; PaymentKind = paymentKind ?? (method == BillingPaymentMethod.BankTransfer ? BillingPaymentKind.BankTransfer : BillingPaymentKind.InitialCheckout);
        Status = method == BillingPaymentMethod.Card ? BillingOrderStatus.Pending : BillingOrderStatus.AwaitingTransfer;
        CreatedAt = nowUtc; ExpiresAt = nowUtc.AddHours(24); SubscriptionFromUtc = fromUtc; SubscriptionThroughUtc = throughUtc;
        BankReference = method == BillingPaymentMethod.BankTransfer ? $"LC-{id:N}"[..15].ToUpperInvariant() : null;
    }
    public void AttachCheckout(string id) => ExternalCheckoutId = id;
    public void AttachReceipt(string url) => ReceiptUrl = url;
    public bool MarkPaid(string? approvedBy, DateTime nowUtc) { if (Status == BillingOrderStatus.Paid) return false; Status = BillingOrderStatus.Paid; ApprovedBy = approvedBy; ApprovedAt = approvedBy is null ? null : nowUtc; return true; }
    public bool MarkExpired() { if (Status != BillingOrderStatus.Pending) return false; Status = BillingOrderStatus.Expired; return true; }
    public void MarkFailed() { if (Status != BillingOrderStatus.Paid) Status = BillingOrderStatus.Failed; }
    public void Reject(string by, DateTime nowUtc) { if (Status != BillingOrderStatus.AwaitingTransfer) throw new InvalidOperationException(); Status = BillingOrderStatus.Rejected; ApprovedBy = by; ApprovedAt = nowUtc; }
}
