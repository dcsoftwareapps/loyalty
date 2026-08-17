using LoyaltyCloud.Application.Billing;
using LoyaltyCloud.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class StripePaymentGateway : IPaymentGateway
{
    private readonly StripeOptions _options;
    public StripePaymentGateway(IOptions<StripeOptions> options) => _options = options.Value;
    public bool IsAvailable => _options.IsConfigured;
    public async Task<CheckoutGatewayResult> CreateCheckoutAsync(CheckoutGatewayRequest r, CancellationToken ct = default)
    {
        if (!IsAvailable) throw new InvalidOperationException("Stripe no está configurado.");
        var client = new StripeClient(_options.SecretKey);
        var service = new SessionService(client);
        var session = await service.CreateAsync(new SessionCreateOptions
        {
            Mode = "payment", SuccessUrl = r.SuccessUrl, CancelUrl = r.CancelUrl,
            ClientReferenceId = r.OrderId.ToString(),
            Metadata = new Dictionary<string,string>{{"BillingOrderId",r.OrderId.ToString()},{"TenantId",r.TenantId.ToString()}},
            LineItems = [new SessionLineItemOptions { Quantity=1, PriceData=new SessionLineItemPriceDataOptions { Currency=r.Currency.ToLowerInvariant(), UnitAmount=r.AmountMinor, ProductData=new SessionLineItemPriceDataProductDataOptions{Name=r.Description} } }]
        }, cancellationToken: ct);
        return new(session.Id, session.Url);
    }
    public StripePaymentConfirmation ParseWebhook(string payload, string signature)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret)) throw new InvalidOperationException("Stripe webhook no configurado.");
        var evt = EventUtility.ConstructEvent(payload, signature, _options.WebhookSecret);
        if (evt.Data.Object is not Session s) return new(evt.Id, evt.Type, "", "", Guid.Empty, Guid.Empty, 0, "", false, null, null);
        Guid.TryParse(s.Metadata.GetValueOrDefault("BillingOrderId"), out var orderId); Guid.TryParse(s.Metadata.GetValueOrDefault("TenantId"), out var tenantId);
        return new(evt.Id, evt.Type, s.Id, s.PaymentIntentId ?? s.Id, orderId, tenantId, s.AmountTotal ?? 0, s.Currency ?? "", s.PaymentStatus == "paid", null, null);
    }
}
