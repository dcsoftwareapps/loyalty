using System.Security.Cryptography;
using LoyaltyCloud.Domain.Common;
using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Domain.Entities;

public sealed class GiftCardConfiguration : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }
    public bool IsEnabled { get; private set; }
    public bool AllowCustomAmount { get; private set; }
    public bool AllowPartialRedemption { get; private set; }
    public bool AllowPromotionalIssuance { get; private set; }
    public GiftCardExpirationMode ExpirationMode { get; private set; }
    public int? DefaultExpirationMonths { get; private set; }
    public string Currency { get; private set; } = "MXN";
    public string DisplayName { get; private set; } = "Gift Card";
    public string PrimaryColor { get; private set; } = "#1C1B18";
    public string TextColor { get; private set; } = "#FFFFFF";
    public string? LogoUrl { get; private set; }
    public string? SecondaryText { get; private set; }
    public string? Terms { get; private set; }
    public string? FooterMessage { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    private GiftCardConfiguration() { }
    public GiftCardConfiguration(Guid id, Guid tenantId, DateTime nowUtc) : base(id) { TenantId = tenantId; UpdatedAtUtc = nowUtc; }
    public void Update(bool enabled, bool custom, bool partial, bool promotional, GiftCardExpirationMode expirationMode, int? months, string currency, string displayName, string primaryColor, string textColor, string? logoUrl, string? secondaryText, string? terms, string? footer, DateTime nowUtc)
    {
        if (tenantIdInvalid(TenantId)) throw new InvalidOperationException("Tenant inválido.");
        if (expirationMode == GiftCardExpirationMode.MonthsAfterIssue && (months is null or < 1 or > 120)) throw new ArgumentException("Meses de expiración inválidos.");
        IsEnabled = enabled; AllowCustomAmount = custom; AllowPartialRedemption = partial; AllowPromotionalIssuance = promotional;
        ExpirationMode = expirationMode; DefaultExpirationMonths = expirationMode == GiftCardExpirationMode.MonthsAfterIssue ? months : null;
        Currency = Normalize(currency, 3, "MXN").ToUpperInvariant(); DisplayName = Normalize(displayName, 100, "Gift Card");
        PrimaryColor = Normalize(primaryColor, 7, "#1C1B18"); TextColor = Normalize(textColor, 7, "#FFFFFF"); LogoUrl = Trim(logoUrl, 500);
        SecondaryText = Trim(secondaryText, 200); Terms = Trim(terms, 2000); FooterMessage = Trim(footer, 300); UpdatedAtUtc = nowUtc;
    }
    private static bool tenantIdInvalid(Guid id) => id == Guid.Empty;
    private static string Normalize(string? value, int max, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim()[..Math.Min(value.Trim().Length, max)];
    private static string? Trim(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];
}

public sealed class GiftCardDenomination : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "MXN";
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    private GiftCardDenomination() { }
    public GiftCardDenomination(Guid id, Guid tenantId, decimal amount, string currency, DateTime nowUtc) : base(id) { if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount)); TenantId = tenantId; Amount = decimal.Round(amount, 2); Currency = currency.Trim().ToUpperInvariant(); IsActive = true; CreatedAtUtc = nowUtc; }
    public void SetActive(bool active) => IsActive = active;
}

public sealed class GiftCard : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }
    public string PublicCode { get; private set; } = string.Empty;
    public string ClaimTokenHash { get; private set; } = string.Empty;
    public bool ClaimRevoked { get; private set; }
    public decimal InitialValue { get; private set; }
    public decimal CurrentBalance { get; private set; }
    public string Currency { get; private set; } = "MXN";
    public GiftCardStatus Status { get; private set; }
    public Guid? RecipientMemberId { get; private set; }
    public string RecipientName { get; private set; } = string.Empty;
    public string? RecipientEmail { get; private set; }
    public string? RecipientPhone { get; private set; }
    public string? SenderName { get; private set; }
    public string? PersonalMessage { get; private set; }
    public GiftCardSource Source { get; private set; }
    public Guid IssuedByUserId { get; private set; }
    public DateTime IssuedAtUtc { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    private GiftCard() { }
    public GiftCard(Guid id, Guid tenantId, string code, string claimTokenHash, decimal value, string currency, Guid? memberId, string recipientName, string? email, string? phone, string? sender, string? message, GiftCardSource source, Guid issuedBy, DateTime issuedAt, DateTime? expiresAt) : base(id)
    {
        if (tenantId == Guid.Empty || issuedBy == Guid.Empty) throw new ArgumentException("Tenant y emisor requeridos."); if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
        TenantId = tenantId; PublicCode = code; ClaimTokenHash = claimTokenHash; InitialValue = CurrentBalance = decimal.Round(value, 2); Currency = currency.Trim().ToUpperInvariant();
        RecipientMemberId = memberId; RecipientName = Required(recipientName, 150); RecipientEmail = Optional(email, 254); RecipientPhone = Optional(phone, 30); SenderName = Optional(sender, 150); PersonalMessage = Optional(message, 500);
        Source = source; IssuedByUserId = issuedBy; IssuedAtUtc = CreatedAtUtc = UpdatedAtUtc = issuedAt; ExpiresAtUtc = expiresAt; Status = expiresAt <= issuedAt ? GiftCardStatus.Expired : GiftCardStatus.Active;
    }
    public (decimal Before, decimal After) Redeem(decimal amount, bool partialAllowed, DateTime nowUtc)
    {
        EvaluateExpiration(nowUtc); EnsureActive(); amount = decimal.Round(amount, 2); if (amount <= 0 || amount > CurrentBalance) throw new InvalidOperationException("Saldo insuficiente o monto inválido.");
        if (!partialAllowed && amount != CurrentBalance) throw new InvalidOperationException("Esta Gift Card solo permite canje completo.");
        var before = CurrentBalance; CurrentBalance -= amount; if (CurrentBalance == 0) Status = GiftCardStatus.FullyRedeemed; UpdatedAtUtc = nowUtc; return (before, CurrentBalance);
    }
    public (decimal Before, decimal After) Adjust(decimal amount, DateTime nowUtc) { EvaluateExpiration(nowUtc); EnsureActive(); if (amount == 0 || CurrentBalance + amount < 0) throw new InvalidOperationException("El ajuste produciría un saldo inválido."); var before = CurrentBalance; CurrentBalance = decimal.Round(CurrentBalance + amount, 2); if (CurrentBalance == 0) Status = GiftCardStatus.FullyRedeemed; UpdatedAtUtc = nowUtc; return (before, CurrentBalance); }
    public decimal Cancel(DateTime nowUtc) { EvaluateExpiration(nowUtc); EnsureActive(); Status = GiftCardStatus.Cancelled; ClaimRevoked = true; UpdatedAtUtc = nowUtc; return CurrentBalance; }
    public decimal EvaluateExpiration(DateTime nowUtc) { if (Status == GiftCardStatus.Active && ExpiresAtUtc is { } expires && expires <= nowUtc) { Status = GiftCardStatus.Expired; UpdatedAtUtc = nowUtc; return CurrentBalance; } return 0; }
    public void RevokeClaim(DateTime nowUtc) { ClaimRevoked = true; UpdatedAtUtc = nowUtc; }
    private void EnsureActive() { if (Status != GiftCardStatus.Active) throw new InvalidOperationException("La Gift Card no está activa."); }
    private static string Required(string value, int max) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Valor requerido.") : value.Trim()[..Math.Min(value.Trim().Length, max)];
    private static string? Optional(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];
    public static string HashClaimToken(string token) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}

public sealed class GiftCardTransaction : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; } public Guid GiftCardId { get; private set; } public GiftCardTransactionType Type { get; private set; }
    public decimal Amount { get; private set; } public decimal BalanceBefore { get; private set; } public decimal BalanceAfter { get; private set; }
    public Guid PerformedByUserId { get; private set; } public DateTime CreatedAtUtc { get; private set; } public string? Reference { get; private set; } public string? Notes { get; private set; } public string? IdempotencyKey { get; private set; }
    private GiftCardTransaction() { }
    public GiftCardTransaction(Guid id, Guid tenantId, Guid cardId, GiftCardTransactionType type, decimal amount, decimal before, decimal after, Guid userId, DateTime nowUtc, string? reference = null, string? notes = null, string? idempotencyKey = null) : base(id) { TenantId=tenantId;GiftCardId=cardId;Type=type;Amount=decimal.Round(amount,2);BalanceBefore=before;BalanceAfter=after;PerformedByUserId=userId;CreatedAtUtc=nowUtc;Reference=reference;Notes=notes;IdempotencyKey=idempotencyKey; }
}

public sealed class GiftCardWallet : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; } public Guid GiftCardId { get; private set; } public GiftCardWalletProvider Provider { get; private set; }
    public string ExternalClassId { get; private set; } = string.Empty; public string ExternalObjectId { get; private set; } = string.Empty; public string? AuthenticationToken { get; private set; } public GiftCardWalletStatus Status { get; private set; }
    public DateTime? LastSynchronizedAtUtc { get; private set; } public string? LastError { get; private set; } public DateTime UpdatedAtUtc { get; private set; }
    private GiftCardWallet() { }
    public GiftCardWallet(Guid id, Guid tenantId, Guid cardId, GiftCardWalletProvider provider, string classId, string objectId, DateTime now, string? authenticationToken = null) : base(id) { TenantId=tenantId;GiftCardId=cardId;Provider=provider;ExternalClassId=classId;ExternalObjectId=objectId;AuthenticationToken=authenticationToken;Status=GiftCardWalletStatus.Pending;UpdatedAtUtc=now; }
    public void Synced(DateTime now){Status=GiftCardWalletStatus.Active;LastSynchronizedAtUtc=now;LastError=null;UpdatedAtUtc=now;} public void Failed(string error,DateTime now){Status=GiftCardWalletStatus.Error;LastError=error[..Math.Min(error.Length,1000)];UpdatedAtUtc=now;} public void Pending(DateTime now){Status=GiftCardWalletStatus.SyncPending;UpdatedAtUtc=now;}
}
