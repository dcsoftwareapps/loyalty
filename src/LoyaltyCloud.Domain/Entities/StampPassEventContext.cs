using LoyaltyCloud.Domain.Common;

namespace LoyaltyCloud.Domain.Entities;

/// <summary>One bounded announcement context per Stamps card. GETs never consume it.</summary>
public sealed class StampPassEventContext : Entity, ITenantOwned
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);
    public Guid TenantId { get; private set; }
    public string SerialNumber { get; private set; } = string.Empty;
    public Guid TransactionId { get; private set; }
    public bool IsRollover { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public bool AnnouncementEnabled { get; private set; }
    private StampPassEventContext() { }
    public StampPassEventContext(string serial, StampTransaction transaction, bool rollover)
        : base(Guid.NewGuid())
    {
        SerialNumber = serial;
        TenantId = transaction.TenantId;
        SetEvent(transaction, rollover);
    }
    public void SetEvent(StampTransaction transaction, bool rollover)
    {
        if (transaction.TenantId != TenantId || transaction.Type == Enums.StampTransactionType.Welcome)
            throw new ArgumentException("Invalid Stamps announcement event.");
        if (TransactionId == transaction.Id) return; // Never reactivate the same event.
        TransactionId = transaction.Id;
        IsRollover = rollover;
        ExpiresAtUtc = transaction.CreatedAt.Add(Lifetime);
        AnnouncementEnabled = true;
    }
    public void Suppress() => AnnouncementEnabled = false;
}
