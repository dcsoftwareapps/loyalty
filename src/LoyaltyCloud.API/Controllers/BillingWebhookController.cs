using LoyaltyCloud.Application.Billing;
using Microsoft.AspNetCore.Mvc;

namespace LoyaltyCloud.API.Controllers;

[ApiController]
[Route("api/billing/webhooks/stripe")]
public sealed class BillingWebhookController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Stripe([FromServices] IBillingService billing, CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(ct);
        var signature = Request.Headers["Stripe-Signature"].ToString();
        if (string.IsNullOrWhiteSpace(signature)) return BadRequest();
        try { await billing.ProcessStripeWebhookAsync(payload, signature, ct); return Ok(); }
        catch (Exception) { return BadRequest(); }
    }
}
