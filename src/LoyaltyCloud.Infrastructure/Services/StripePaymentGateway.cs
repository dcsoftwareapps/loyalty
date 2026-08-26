using LoyaltyCloud.Application.Billing;
using LoyaltyCloud.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using System.Text.Json;

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
            Mode = r.Recurring ? "subscription" : "payment", SuccessUrl = r.SuccessUrl, CancelUrl = r.CancelUrl,
            Customer = r.CustomerId,
            SubscriptionData = r.Recurring ? new SessionSubscriptionDataOptions { Metadata = new Dictionary<string,string>{{"BillingOrderId",r.OrderId.ToString()},{"TenantId",r.TenantId.ToString()},{"Months",r.Months.ToString()}} } : null,
            ClientReferenceId = r.OrderId.ToString(),
            Metadata = new Dictionary<string,string>{{"BillingOrderId",r.OrderId.ToString()},{"TenantId",r.TenantId.ToString()}},
            LineItems = [r.Recurring ? new SessionLineItemOptions { Quantity=1, Price=r.PriceId ?? throw new InvalidOperationException("Stripe Price no configurado para el periodo.") } : new SessionLineItemOptions { Quantity=1, PriceData=new SessionLineItemPriceDataOptions { Currency=r.Currency.ToLowerInvariant(), UnitAmount=r.AmountMinor, ProductData=new SessionLineItemPriceDataProductDataOptions{Name=r.Description} } }]
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
    public async Task SetSubscriptionCancellationAsync(string subscriptionId, bool cancelAtPeriodEnd, CancellationToken ct = default)
    {
        if (!IsAvailable) throw new InvalidOperationException("Stripe no está configurado.");
        await new SubscriptionService(new StripeClient(_options.SecretKey)).UpdateAsync(subscriptionId, new SubscriptionUpdateOptions { CancelAtPeriodEnd = cancelAtPeriodEnd }, cancellationToken: ct);
    }
    public StripePaymentConfirmation ParseWebhook(string payload, string signature)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret)) throw new InvalidOperationException("Stripe webhook no configurado.");
        var evt = EventUtility.ConstructEvent(payload, signature, _options.WebhookSecret);
        using var doc = JsonDocument.Parse(payload); var obj = doc.RootElement.GetProperty("data").GetProperty("object");
        string? S(string name) => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        long L(string name) => obj.TryGetProperty(name, out var v) && v.TryGetInt64(out var n) ? n : 0;
        bool B(string name) => obj.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False && v.GetBoolean();
        string? M(string name)
        {
            if (obj.TryGetProperty("metadata", out var m) && m.TryGetProperty(name, out var v)) return v.GetString();
            if (obj.TryGetProperty("subscription_details", out var d) && d.TryGetProperty("metadata", out m) && m.TryGetProperty(name, out v)) return v.GetString();
            return null;
        }
        Guid.TryParse(M("BillingOrderId"), out var orderId); Guid.TryParse(M("TenantId"), out var tenantId);
        var subscriptionId = S("subscription");
        if (subscriptionId is null && obj.TryGetProperty("parent", out var parent) && parent.TryGetProperty("subscription_details", out var details) && details.TryGetProperty("subscription", out var sub)) subscriptionId = sub.GetString();
        DateTime? periodEnd = null;
        if (obj.TryGetProperty("current_period_end", out var pe) && pe.TryGetInt64(out var unix)) periodEnd = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
        else if (obj.TryGetProperty("lines", out var lines) && lines.TryGetProperty("data", out var data) && data.GetArrayLength() > 0 && data[0].TryGetProperty("period", out var period) && period.TryGetProperty("end", out pe) && pe.TryGetInt64(out unix)) periodEnd = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
        var paid = S("payment_status") == "paid" || S("status") == "paid";
        return new(evt.Id,evt.Type,S("id") ?? "",S("payment_intent") ?? S("id") ?? "",orderId,tenantId,L("amount_total") is var total && total > 0 ? total : (paid ? L("amount_paid") : L("amount_due")),S("currency") ?? "",paid,null,null,S("customer"),subscriptionId,evt.Type.StartsWith("invoice.",StringComparison.Ordinal)?S("id"):S("invoice"),S("status"),periodEnd,B("cancel_at_period_end"),S("billing_reason"));
    }
}
