namespace KBeauty.Loyalty.Domain.Enums;

public enum NotificationDeliveryStatus
{
    Pending = 0,
    Processing = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4,
    Unsupported = 5,
    NoRecipients = 6
}
