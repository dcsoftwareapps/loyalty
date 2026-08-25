namespace LoyaltyCloud.Domain.Entities;

public static class NotificationDeliveryFailureReasons
{
    public const string TransientApnsFailurePrefix = "Transient APNs failure";
    public const string PermanentApnsFailurePrefix = "Permanent APNs failure";
    public const string UnsupportedApnsPrefix = "APNs unsupported";
}
