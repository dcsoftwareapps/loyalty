using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Application.Billing;

public sealed record BillingSettingsDto(string Currency, decimal TaxRate, bool PricesIncludeTax, int GracePeriodDays,
    bool CardPaymentsEnabled, bool BankTransferEnabled, bool RequireTransferReceipt, bool AutomaticRenewalEnabled,
    bool CfdiEnabled, string? BankName, string? BeneficiaryName, string? Clabe, string? BankTransferInstructions, string? SupportEmail);
public sealed record BillingEmailSettingsDto(bool Enabled, string Provider, string? FromAddress, string FromName, string? ApplicationBaseUrl, bool CredentialsConfigured, bool IsComplete);
public sealed record SubscriptionPlanDto(Guid Id, string Code, string Name, string Currency, decimal OneMonthPrice,
    decimal ThreeMonthPrice, decimal SixMonthPrice, decimal TwelveMonthPrice, bool IsActive, string? StripeOneMonthPriceId = null, string? StripeThreeMonthPriceId = null, string? StripeSixMonthPriceId = null, string? StripeTwelveMonthPriceId = null);
public sealed record BillingQuoteDto(decimal Subtotal, decimal Tax, decimal Total, string Currency);
public sealed record BillingOrderDto(Guid Id, Guid TenantId, string PlanCode, int Months, decimal Subtotal, decimal Tax,
    decimal Total, string Currency, BillingOrderStatus Status, BillingPaymentMethod PaymentMethod, DateTime CreatedAt,
    DateTime SubscriptionThroughUtc, string? CheckoutUrl, string? BankReference, string? ReceiptUrl, BillingPaymentKind PaymentKind = BillingPaymentKind.InitialCheckout);
public sealed record TenantBillingDto(Guid TenantId, string TenantSlug, string TenantName, string PlanCode,
    string SubscriptionStatus, DateTime? PaidThroughUtc, DateTime? GracePeriodEndsAt, BillingSettingsDto Settings,
    bool CardPaymentsAvailable, IReadOnlyList<SubscriptionPlanDto> Plans, IReadOnlyList<BillingOrderDto> Orders, bool AutoRenewEnabled = true, string? BillingContactEmail = null, string? StripeSubscriptionStatus = null, DateTime? NextChargeUtc = null, bool CancelAtPeriodEnd = false, decimal? RecurringAmount = null, string? RecurringCurrency = null, string? CardBrand = null, string? CardLast4 = null);
public sealed record BillingPaymentResultDto(BillingOrderStatus Status, DateTime? PaidThroughUtc, bool TenantOperational);
public sealed record CheckoutGatewayRequest(Guid OrderId, Guid TenantId, string Description, long AmountMinor,
    string Currency, string SuccessUrl, string CancelUrl, bool Recurring = false, string? CustomerId = null, string? PriceId = null, int Months = 1);
public sealed record CheckoutGatewayResult(string SessionId, string Url);
public enum CheckoutSessionStatus { Open, Complete, Expired, Unknown }
public sealed record CheckoutSessionSnapshot(CheckoutSessionStatus Status, string PaymentStatus);
public sealed record StripePaymentConfirmation(string EventId, string EventType, string SessionId, string PaymentIntentId,
    Guid OrderId, Guid TenantId, long AmountTotalMinor, string Currency, bool Paid, string? CardBrand, string? CardLast4, string? CustomerId = null, string? SubscriptionId = null, string? InvoiceId = null, string? SubscriptionStatus = null, DateTime? PeriodEndUtc = null, bool CancelAtPeriodEnd = false, string? BillingReason = null);

public interface IPaymentGateway
{
    bool IsAvailable { get; }
    Task<CheckoutGatewayResult> CreateCheckoutAsync(CheckoutGatewayRequest request, CancellationToken ct = default);
    Task<CheckoutSessionSnapshot> GetCheckoutSessionAsync(string sessionId, CancellationToken ct = default);
    Task SetSubscriptionCancellationAsync(string subscriptionId, bool cancelAtPeriodEnd, CancellationToken ct = default) => Task.CompletedTask;
    StripePaymentConfirmation ParseWebhook(string payload, string signature);
}

public interface IBillingService
{
    Task<BillingSettingsDto> GetSettingsAsync(CancellationToken ct = default);
    Task SaveSettingsAsync(BillingSettingsDto settings, CancellationToken ct = default);
    Task<BillingEmailSettingsDto> GetEmailSettingsAsync(CancellationToken ct = default);
    Task SaveEmailSettingsAsync(BillingEmailSettingsDto settings, CancellationToken ct = default);
    Task<IReadOnlyList<SubscriptionPlanDto>> GetPlansAsync(bool activeOnly = false, CancellationToken ct = default);
    Task<int> SavePlanAsync(SubscriptionPlanDto plan, CancellationToken ct = default);
    Task<TenantBillingDto> GetTenantBillingAsync(Guid tenantId, CancellationToken ct = default);
    Task<BillingQuoteDto> QuoteAsync(Guid tenantId, string planCode, int months, CancellationToken ct = default);
    Task<BillingOrderDto> CreateOrderAsync(Guid tenantId, string planCode, int months, BillingPaymentMethod method,
        string baseUrl, CancellationToken ct = default);
    Task<BillingOrderDto?> GetOrderAsync(Guid tenantId, Guid orderId, CancellationToken ct = default);
    Task<BillingPaymentResultDto?> GetPaymentResultAsync(string tenantSlug, string token, CancellationToken ct = default);
    Task<IReadOnlyList<BillingOrderDto>> GetAwaitingTransfersAsync(CancellationToken ct = default);
    Task ApproveTransferAsync(Guid orderId, string approvedBy, CancellationToken ct = default);
    Task RejectTransferAsync(Guid orderId, string rejectedBy, CancellationToken ct = default);
    Task UpdateAutoRenewAsync(Guid tenantId, bool enabled, string? billingContactEmail = null, CancellationToken ct = default);
    Task ProcessStripeWebhookAsync(string payload, string signature, CancellationToken ct = default);
}
