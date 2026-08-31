namespace LoyaltyCloud.Domain.Enums;

public enum GiftCardStatus { Active, FullyRedeemed, Expired, Cancelled }
public enum GiftCardSource { PurchasedExternally, Promotional, Manual, Compensation }
public enum GiftCardTransactionType { Issued, Redeemed, AdjustmentCredit, AdjustmentDebit, Expired, Cancelled }
public enum GiftCardExpirationMode { Never, MonthsAfterIssue, SelectAtIssue }
public enum GiftCardWalletProvider { Apple, Google }
public enum GiftCardWalletStatus { Pending, Active, SyncPending, Error, Revoked }
