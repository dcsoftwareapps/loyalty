using System.ComponentModel.DataAnnotations;
using LoyaltyCloud.API.Auth;
using LoyaltyCloud.Application.GiftCards;
using LoyaltyCloud.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoyaltyCloud.API.Controllers;

[ApiController]
[Route("api/giftcards")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = CashierAuthDefaults.AuthenticationScheme,
    Policy = CashierAuthorizationPolicies.CashierOperations)]
public sealed class GiftCardsController(IGiftCardService giftCards, IGiftCardDeliveryService delivery) : ControllerBase
{
    [HttpGet("issue/options")]
    public async Task<IActionResult> IssueOptions(CancellationToken ct)
    {
        var settings = await giftCards.GetSettingsAsync(ct);
        if (!settings.IsEnabled)
            return Error(403, GiftCardFailure.Unavailable, "El módulo de tarjetas de regalo no está disponible.");

        return Ok(new IssueOptionsResponse(settings.Currency, settings.AllowCustomAmount,
            settings.ExpirationMode.ToString(), settings.DefaultExpirationMonths,
            settings.Denominations.Where(x => x.IsActive)
                .OrderBy(x => x.Amount)
                .Select(x => new IssueDenomination(x.Amount, x.Currency))
                .ToList()));
    }

    [HttpPost("issue")]
    public async Task<IActionResult> Issue(IssueRequest request, CancellationToken ct)
    {
        try
        {
            var issued = await giftCards.IssueAsync(new IssueGiftCardRequest(request.Amount, null,
                request.RecipientName, request.RecipientEmail, null, request.SenderName,
                request.PersonalMessage, GiftCardSource.Manual, request.ExpiresAtUtc,
                request.IdempotencyKey), ct);
            var settings = await giftCards.GetSettingsAsync(ct);
            var claimUrl = await delivery.GetClaimUrlAsync(issued.ClaimToken, ct);
            return Ok(new IssueResponse(Summarize(issued.Card, settings.AllowPartialRedemption), claimUrl));
        }
        catch (GiftCardUnavailableException ex) { return Error(403, GiftCardFailure.Unavailable, ex.Message); }
        catch (ArgumentException ex) { return Error(400, GiftCardFailure.InvalidInput, ex.Message); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("idempotencia", StringComparison.OrdinalIgnoreCase))
        {
            return Error(409, GiftCardFailure.IdempotencyConflict, ex.Message);
        }
        catch (InvalidOperationException ex) { return Error(400, GiftCardFailure.InvalidInput, ex.Message); }
    }

    [HttpPost("lookup")]
    public async Task<IActionResult> Lookup(LookupRequest request, CancellationToken ct)
    {
        var code = request.Code?.Trim();
        var token = request.ClaimToken?.Trim();
        if (string.IsNullOrEmpty(code) == string.IsNullOrEmpty(token))
            return Error(400, GiftCardFailure.InvalidInput, "Indica código o claimToken, pero no ambos.");
        try
        {
            var detail = !string.IsNullOrEmpty(code)
                ? await giftCards.GetByCodeAsync(code, ct)
                : await giftCards.GetByClaimTokenAsync(token!, ct);
            if (detail is null)
                return Error(404, GiftCardFailure.NotFound, "Tarjeta de regalo no encontrada.");
            var settings = await giftCards.GetSettingsAsync(ct);
            return Ok(Summarize(detail.Card, settings.AllowPartialRedemption));
        }
        catch (GiftCardUnavailableException ex) { return Error(403, GiftCardFailure.Unavailable, ex.Message); }
    }

    [HttpPost("{code}/redeem")]
    public async Task<IActionResult> Redeem(
        [RegularExpression(@"(?i)^GC-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}$")] string code,
        RedeemRequest request, CancellationToken ct)
    {
        try
        {
            var result = await giftCards.RedeemAsync(code.Trim(), request.Amount,
                request.IdempotencyKey, request.Reference?.Trim(), null, ct);
            if (!result.Success)
            {
                var failure = result.Failure ?? GiftCardFailure.InvalidInput;
                var status = failure switch
                {
                    GiftCardFailure.NotFound => 404,
                    GiftCardFailure.IdempotencyConflict or GiftCardFailure.ConcurrencyConflict => 409,
                    GiftCardFailure.InvalidInput => 400,
                    _ => 422
                };
                return Error(status, failure, result.Error ?? "No fue posible completar el canje.");
            }
            var settings = await giftCards.GetSettingsAsync(ct);
            return Ok(new RedeemResponse(result.RedeemedAmount!.Value,
                Summarize(result.Detail!.Card, settings.AllowPartialRedemption), result.WasIdempotent));
        }
        catch (GiftCardUnavailableException ex) { return Error(403, GiftCardFailure.Unavailable, ex.Message); }
        catch (ArgumentException ex) { return Error(400, GiftCardFailure.InvalidInput, ex.Message); }
    }

    private ObjectResult Error(int status, GiftCardFailure code, string detail) =>
        new(new ProblemDetails
        {
            Status = status, Title = "Gift Card", Detail = detail,
            Extensions = { ["code"] = code.ToString() }
        }) { StatusCode = status };

    private static CardSummary Summarize(GiftCardDto card, bool partial) =>
        new(card.Code, card.InitialValue, card.CurrentBalance, card.Currency,
            card.Status.ToString(), card.ExpiresAtUtc, partial, card.RecipientName);

    public sealed record LookupRequest(
        [StringLength(32), RegularExpression(@"(?i)^\s*GC-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}\s*$")] string? Code,
        [StringLength(256), RegularExpression(@"^\s*[A-Za-z0-9_-]+\s*$")] string? ClaimToken);

    public sealed record RedeemRequest(decimal Amount,
        [Required, StringLength(100)] string IdempotencyKey,
        [StringLength(200)] string? Reference);
    public sealed record IssueRequest(decimal Amount,
        [Required, StringLength(150)] string RecipientName,
        [StringLength(254), EmailAddress] string? RecipientEmail,
        [StringLength(150)] string? SenderName,
        [StringLength(500)] string? PersonalMessage,
        DateTime? ExpiresAtUtc,
        [Required, StringLength(100)] string IdempotencyKey);
    public sealed record IssueDenomination(decimal Amount, string Currency);
    public sealed record IssueOptionsResponse(string Currency, bool AllowCustomAmount,
        string ExpirationMode, int? DefaultExpirationMonths, IReadOnlyList<IssueDenomination> Denominations);

    public sealed record CardSummary(string Code, decimal InitialBalance, decimal RemainingBalance,
        string Currency, string Status, DateTime? ExpiresAtUtc, bool AllowPartialRedemption, string RecipientName);
    public sealed record RedeemResponse(decimal RedeemedAmount, CardSummary Card, bool WasIdempotent);
    public sealed record IssueResponse(CardSummary Card, string? ClaimUrl);
}
