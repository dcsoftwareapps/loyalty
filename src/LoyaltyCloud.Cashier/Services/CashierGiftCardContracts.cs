using System.Text.Json.Serialization;

namespace LoyaltyCloud.Cashier.Services;

public sealed record GiftCardLookup(string? Code, string? ClaimToken);
public sealed record GiftCardRedemption(decimal Amount, string IdempotencyKey, string? Reference = null);
public sealed record CashierGiftCard(
    [property: JsonRequired] string Code,
    [property: JsonRequired] decimal InitialBalance,
    [property: JsonRequired] decimal RemainingBalance,
    [property: JsonRequired] string Currency,
    [property: JsonRequired] string Status,
    DateTimeOffset? ExpiresAtUtc,
    [property: JsonRequired] bool AllowPartialRedemption,
    string? RecipientName);
public sealed record GiftCardReceipt(
    [property: JsonRequired] decimal RedeemedAmount,
    [property: JsonRequired] CashierGiftCard Card,
    [property: JsonRequired] bool WasIdempotent);

public sealed record GiftCardResult<T>(T? Value, string? Error = null, string? Message = null, bool DefinitiveRejection = false)
{
    public bool Succeeded => Value is not null && Error is null;
    public static GiftCardResult<T> Fail(string error, string message, bool definitive = false) => new(default, error, message, definitive);
}

public interface ICashierGiftCardApi
{
    Task<GiftCardResult<CashierGiftCard>> LookupAsync(GiftCardLookup lookup);
    Task<GiftCardResult<GiftCardReceipt>> RedeemAsync(string code, GiftCardRedemption request);
}
