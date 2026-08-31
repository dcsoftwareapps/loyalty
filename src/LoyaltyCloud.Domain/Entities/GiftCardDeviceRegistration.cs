using LoyaltyCloud.Domain.Common;

namespace LoyaltyCloud.Domain.Entities;

public sealed class GiftCardDeviceRegistration : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }
    public Guid GiftCardId { get; private set; }
    public string DeviceLibraryIdentifier { get; private set; } = string.Empty;
    public string PassTypeIdentifier { get; private set; } = string.Empty;
    public string SerialNumber { get; private set; } = string.Empty;
    public string PushToken { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private GiftCardDeviceRegistration() { }

    public GiftCardDeviceRegistration(Guid id, Guid tenantId, Guid giftCardId, string deviceId, string passTypeId, string serial, string pushToken, DateTime nowUtc) : base(id)
    {
        if (tenantId == Guid.Empty || giftCardId == Guid.Empty) throw new ArgumentException("Tenant y Gift Card requeridos.");
        TenantId = tenantId; GiftCardId = giftCardId;
        DeviceLibraryIdentifier = Required(deviceId, 100); PassTypeIdentifier = Required(passTypeId, 100);
        SerialNumber = Required(serial, 128); PushToken = Required(pushToken, 500);
        CreatedAtUtc = UpdatedAtUtc = nowUtc;
    }

    public void UpdatePushToken(string token, DateTime nowUtc) { PushToken = Required(token, 500); UpdatedAtUtc = nowUtc; }
    private static string Required(string value, int max) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Valor requerido.") : value.Trim()[..Math.Min(value.Trim().Length, max)];
}
