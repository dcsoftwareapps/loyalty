using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Application.GiftCards;

public sealed record GiftCardSettingsDto(Guid Id, bool IsEnabled, bool AllowCustomAmount, bool AllowPartialRedemption, bool AllowPromotionalIssuance, GiftCardExpirationMode ExpirationMode, int? DefaultExpirationMonths, string Currency, string DisplayName, string PrimaryColor, string TextColor, string? LogoUrl, string? SecondaryText, string? Terms, string? FooterMessage, IReadOnlyList<GiftCardDenominationDto> Denominations, string? SyncWarning = null);
public sealed record GiftCardDenominationDto(Guid Id, decimal Amount, string Currency, bool IsActive);
public sealed record UpdateGiftCardSettingsRequest(bool IsEnabled, bool AllowCustomAmount, bool AllowPartialRedemption, bool AllowPromotionalIssuance, GiftCardExpirationMode ExpirationMode, int? DefaultExpirationMonths, string Currency, string DisplayName, string PrimaryColor, string TextColor, string? LogoUrl, string? SecondaryText, string? Terms, string? FooterMessage);
public sealed record IssueGiftCardRequest(decimal Amount, Guid? RecipientMemberId, string RecipientName, string? RecipientEmail, string? RecipientPhone, string? SenderName, string? PersonalMessage, GiftCardSource Source = GiftCardSource.Manual, DateTime? ExpiresAtUtc = null);
public sealed record GiftCardDto(Guid Id, string Code, decimal InitialValue, decimal CurrentBalance, string Currency, GiftCardStatus Status, Guid? RecipientMemberId, string RecipientName, string? RecipientEmail, string? RecipientPhone, string? SenderName, string? PersonalMessage, GiftCardSource Source, DateTime IssuedAtUtc, DateTime? ExpiresAtUtc, DateTime UpdatedAtUtc);
public sealed record GiftCardTransactionDto(Guid Id, GiftCardTransactionType Type, decimal Amount, decimal BalanceBefore, decimal BalanceAfter, Guid PerformedByUserId, DateTime CreatedAtUtc, string? Reference, string? Notes, string? IdempotencyKey);
public sealed record GiftCardDetailDto(GiftCardDto Card, IReadOnlyList<GiftCardTransactionDto> Transactions);
public sealed record IssuedGiftCardDto(GiftCardDto Card, string ClaimToken);
public sealed record GiftCardPage(IReadOnlyList<GiftCardDto> Items, int Total, int Page, int PageSize);
public sealed record GiftCardDashboardDto(int ActiveCards, decimal OutstandingBalance, decimal IssuedValue, decimal RedeemedValue, int FullyRedeemedCards, int ExpiredCards, int CancelledCards);
public sealed record GiftCardReportPoint(DateTime DateUtc, decimal Issued, decimal Redeemed, int IssuedCount, int RedemptionCount);
public sealed record GiftCardOperationResult(bool Success, string? Error, GiftCardDetailDto? Detail, bool WasIdempotent = false);
public sealed record GiftCardClaimDto(GiftCardDto Card, string DisplayName, string PrimaryColor, string TextColor, string? LogoUrl, string? SecondaryText, string? Terms, string? FooterMessage);
public enum GiftCardDeliveryStatus { Sent, NotSent, Failed }
public sealed record GiftCardDeliveryResult(GiftCardDeliveryStatus Status, string? Message, string? ClaimUrl)
{
    public bool Sent => Status == GiftCardDeliveryStatus.Sent;
}

public interface IGiftCardService
{
    Task<bool> IsEnabledAsync(CancellationToken ct = default);
    Task SetEnabledAsync(bool enabled, CancellationToken ct = default);
    Task<GiftCardSettingsDto> GetSettingsAsync(CancellationToken ct = default);
    Task<GiftCardSettingsDto> UpdateSettingsAsync(UpdateGiftCardSettingsRequest request, CancellationToken ct = default);
    Task<GiftCardDenominationDto> AddDenominationAsync(decimal amount, CancellationToken ct = default);
    Task SetDenominationActiveAsync(Guid id, bool active, CancellationToken ct = default);
    Task<IssuedGiftCardDto> IssueAsync(IssueGiftCardRequest request, CancellationToken ct = default);
    Task<GiftCardPage> SearchAsync(string? search, GiftCardStatus? status, DateTime? fromUtc, DateTime? toUtc, int page = 1, int pageSize = 25, CancellationToken ct = default);
    Task<GiftCardDetailDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<GiftCardDetailDto?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IssuedGiftCardDto> RotateClaimTokenAsync(Guid id, CancellationToken ct = default);
    Task<GiftCardOperationResult> RedeemAsync(string code, decimal amount, string idempotencyKey, string? reference, string? notes, CancellationToken ct = default);
    Task<GiftCardOperationResult> AdjustAsync(Guid id, decimal amount, string idempotencyKey, string? reference, string? notes, CancellationToken ct = default);
    Task<GiftCardOperationResult> CancelAsync(Guid id, string idempotencyKey, string? notes, CancellationToken ct = default);
    Task<GiftCardDashboardDto> GetDashboardAsync(DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken ct = default);
    Task<IReadOnlyList<GiftCardReportPoint>> GetReportAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
    Task<int> ExpireDueAsync(CancellationToken ct = default);
}

public interface IGiftCardClaimService
{
    Task<GiftCardClaimDto?> GetAsync(string claimToken, CancellationToken ct = default);
    Task<GiftCardApplePassResult> GetApplePassAsync(string claimToken, CancellationToken ct = default);
    Task<GiftCardWalletLinkDto> GetGoogleWalletLinkAsync(string claimToken, CancellationToken ct = default);
}

public interface IGiftCardDeliveryService
{
    Task<string?> GetClaimUrlAsync(string claimToken, CancellationToken ct = default);
    Task<GiftCardDeliveryResult> SendEmailAsync(IssuedGiftCardDto giftCard, string recipient, string businessName, CancellationToken ct = default);
}



public sealed record GiftCardWalletLinkDto(GiftCardWalletProvider Provider, string Url, string ExternalClassId, string ExternalObjectId);
public sealed record GiftCardWalletSyncResult(int Attempted, int Failed);
public interface IGiftCardWalletService
{
    Task<GiftCardWalletLinkDto> GetGoogleSaveLinkAsync(Guid giftCardId, CancellationToken ct = default);
    Task SynchronizeAsync(Guid giftCardId, CancellationToken ct = default);
    Task<GiftCardWalletSyncResult> SynchronizeBrandingAsync(CancellationToken ct = default);
}


public sealed record GiftCardApplePassResult(byte[] Bytes, string SerialNumber, DateTime LastModifiedUtc);
public sealed record GiftCardAppleRegistrationResult(bool Found, bool WasNew);
public sealed record GiftCardAppleUpdates(IReadOnlyList<string> SerialNumbers, DateTime LastUpdatedUtc);
public interface IGiftCardAppleWalletService
{
    Task<GiftCardApplePassResult> CreateOrUpdatePassAsync(Guid giftCardId, CancellationToken ct = default);
    Task<GiftCardApplePassResult?> GetPassAsync(string serialNumber, CancellationToken ct = default);
    Task<bool> AuthenticateAndSetTenantAsync(string serialNumber, string token, CancellationToken ct = default);
    Task<GiftCardAppleRegistrationResult> RegisterAsync(string deviceId, string passTypeId, string serialNumber, string pushToken, CancellationToken ct = default);
    Task<bool> UnregisterAsync(string deviceId, string passTypeId, string serialNumber, CancellationToken ct = default);
    Task<GiftCardAppleUpdates> GetUpdatesAsync(string deviceId, string passTypeId, DateTime? sinceUtc, CancellationToken ct = default);
    Task SynchronizeAsync(Guid giftCardId, CancellationToken ct = default);
}
