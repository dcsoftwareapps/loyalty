using LoyaltyCloud.Domain.Common;
using LoyaltyCloud.Domain.Enums;
namespace LoyaltyCloud.Domain.Entities;
public sealed class PaymentTransaction : Entity, ITenantOwned
{
    public Guid BillingOrderId { get; private set; } public Guid TenantId { get; private set; }
    public PaymentProvider Provider { get; private set; } public string ExternalTransactionId { get; private set; } = string.Empty;
    public BillingPaymentMethod PaymentMethod { get; private set; } public PaymentTransactionStatus Status { get; private set; }
    public decimal Amount { get; private set; } public string Currency { get; private set; } = string.Empty; public DateTime PaidAt { get; private set; }
    public string? FailureCode { get; private set; } public string? FailureMessage { get; private set; } public string? ReceiptUrl { get; private set; }
    public string? CardBrand { get; private set; } public string? CardLast4 { get; private set; } public DateTime CreatedAt { get; private set; }
    private PaymentTransaction() { }
    public PaymentTransaction(Guid id, BillingOrder o, string externalId, DateTime nowUtc, string? brand = null, string? last4 = null) : base(id)
    { BillingOrderId=o.Id; TenantId=o.TenantId; Provider=o.Provider; ExternalTransactionId=externalId; PaymentMethod=o.PaymentMethod; Status=PaymentTransactionStatus.Succeeded; Amount=o.Total; Currency=o.Currency; PaidAt=nowUtc; CreatedAt=nowUtc; ReceiptUrl=o.ReceiptUrl; CardBrand=brand; CardLast4=last4; }
}
