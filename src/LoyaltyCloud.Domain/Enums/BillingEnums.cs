namespace LoyaltyCloud.Domain.Enums;

public enum BillingOrderStatus { Pending, AwaitingTransfer, Processing, Paid, Failed, Expired, Cancelled, Rejected, Refunded }
public enum BillingPaymentMethod { Card, BankTransfer }
public enum PaymentProvider { Manual, Stripe }
public enum PaymentTransactionStatus { Succeeded, Failed, Refunded }
public enum WebhookProcessingStatus { Received, Processed, Failed, Ignored }
