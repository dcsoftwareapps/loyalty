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
public sealed record GiftCardIssueDenomination(
    [property: JsonRequired] decimal Amount,
    [property: JsonRequired] string Currency);
public sealed record GiftCardIssueOptions(
    [property: JsonRequired] string Currency,
    [property: JsonRequired] bool AllowCustomAmount,
    [property: JsonRequired] string ExpirationMode,
    int? DefaultExpirationMonths,
    [property: JsonRequired] IReadOnlyList<GiftCardIssueDenomination> Denominations);
public sealed record GiftCardIssueDraft(decimal Amount, string RecipientName, string? RecipientEmail = null,
    string? SenderName = null, string? PersonalMessage = null, DateTime? ExpiresAtUtc = null);
public sealed record GiftCardIssueRequest(decimal Amount, string RecipientName, string? RecipientEmail,
    string? SenderName, string? PersonalMessage, DateTime? ExpiresAtUtc, string IdempotencyKey);
public sealed record GiftCardIssueReceipt(
    [property: JsonRequired] CashierGiftCard Card,
    string? ClaimUrl);

public sealed record GiftCardResult<T>(T? Value, string? Error = null, string? Message = null, bool DefinitiveRejection = false)
{
    public bool Succeeded => Value is not null && Error is null;
    public static GiftCardResult<T> Fail(string error, string message, bool definitive = false) => new(default, error, message, definitive);
}

public interface ICashierGiftCardApi
{
    Task<GiftCardResult<GiftCardIssueOptions>> GetIssueOptionsAsync();
    Task<GiftCardResult<GiftCardIssueReceipt>> IssueAsync(GiftCardIssueRequest request);
    Task<GiftCardResult<CashierGiftCard>> LookupAsync(GiftCardLookup lookup);
    Task<GiftCardResult<GiftCardReceipt>> RedeemAsync(string code, GiftCardRedemption request);
}
