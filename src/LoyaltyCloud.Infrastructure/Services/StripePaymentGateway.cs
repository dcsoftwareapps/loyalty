using LoyaltyCloud.Application.Billing;
using LoyaltyCloud.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace LoyaltyCloud.Infrastructure.Services;

internal sealed class StripePaymentGateway : IPaymentGateway
{
    private readonly StripeOptions _options;
    private readonly ILogger<StripePaymentGateway> _logger;
    public StripePaymentGateway(IOptions<StripeOptions> options, ILogger<StripePaymentGateway> logger)
    {
        _options = options.Value;
        _logger = logger;
    }
    public bool IsAvailable => _options.IsConfigured;
    public async Task<CheckoutGatewayResult> CreateCheckoutAsync(CheckoutGatewayRequest r, CancellationToken ct = default)
    {
        if (!IsAvailable) throw new InvalidOperationException("Stripe no está configurado.");
        _logger.LogInformation("Creating Stripe Checkout Session. OrderId={OrderId}, TenantId={TenantId}, Currency={Currency}, AmountMinor={AmountMinor}.", r.OrderId, r.TenantId, r.Currency, r.AmountMinor);
        var client = new StripeClient(_options.SecretKey);
        var service = new SessionService(client);
        var session = await service.CreateAsync(new SessionCreateOptions
        {
            Mode = "payment", SuccessUrl = r.SuccessUrl, CancelUrl = r.CancelUrl,
            ClientReferenceId = r.OrderId.ToString(),
            Metadata = new Dictionary<string,string>{{"BillingOrderId",r.OrderId.ToString()},{"TenantId",r.TenantId.ToString()}},
            LineItems = [new SessionLineItemOptions { Quantity=1, PriceData=new SessionLineItemPriceDataOptions { Currency=r.Currency.ToLowerInvariant(), UnitAmount=r.AmountMinor, ProductData=new SessionLineItemPriceDataProductDataOptions{Name=r.Description} } }]
        }, cancellationToken: ct);
        _logger.LogInformation("Stripe Checkout Session created. OrderId={OrderId}, CheckoutUrlPresent={CheckoutUrlPresent}, TestMode={TestMode}.", r.OrderId, !string.IsNullOrWhiteSpace(session.Url), session.Id.StartsWith("cs_test_", StringComparison.Ordinal));
        return new(session.Id, session.Url);
    }
    public async Task<CheckoutSessionSnapshot> GetCheckoutSessionAsync(string sessionId, CancellationToken ct = default)
    {
        if (!IsAvailable) throw new InvalidOperationException("Stripe no está configurado.");
        if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("Session ID requerido.", nameof(sessionId));
        var session = await new SessionService(new StripeClient(_options.SecretKey))
            .GetAsync(sessionId, cancellationToken: ct);
        var status = session.Status switch
        {
            "open" => CheckoutSessionStatus.Open,
            "complete" => CheckoutSessionStatus.Complete,
            "expired" => CheckoutSessionStatus.Expired,
            _ => CheckoutSessionStatus.Unknown
        };
        return new CheckoutSessionSnapshot(status, session.PaymentStatus ?? string.Empty);
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
