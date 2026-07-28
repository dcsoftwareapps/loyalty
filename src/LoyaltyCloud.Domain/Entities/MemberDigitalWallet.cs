using LoyaltyCloud.Domain.Common;
using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Domain.Entities;

/// <summary>
/// Persistent link between a local loyalty card and an external wallet object.
/// It stores provider identifiers and sync status, never JWTs or credentials.
/// </summary>
public sealed class MemberDigitalWallet : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid LoyaltyCardId { get; private set; }
    public DigitalWalletProvider Provider { get; private set; }
    public string ExternalClassId { get; private set; } = string.Empty;
    public string ExternalObjectId { get; private set; } = string.Empty;
    public DigitalWalletStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? LastSynchronizedAt { get; private set; }
    public string? LastSynchronizationError { get; private set; }
    public DateTime? LastSaveLinkCreatedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? MetadataJson { get; private set; }

    private MemberDigitalWallet() { }

    public MemberDigitalWallet(
        Guid id,
        Guid tenantId,
        Guid customerId,
        Guid loyaltyCardId,
        DigitalWalletProvider provider,
        string externalClassId,
        string externalObjectId,
        DateTime createdAtUtc) : base(id)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId requerido.", nameof(tenantId));
        if (customerId == Guid.Empty)
            throw new ArgumentException("CustomerId requerido.", nameof(customerId));
        if (loyaltyCardId == Guid.Empty)
            throw new ArgumentException("LoyaltyCardId requerido.", nameof(loyaltyCardId));
        if (string.IsNullOrWhiteSpace(externalClassId))
            throw new ArgumentException("ExternalClassId requerido.", nameof(externalClassId));
        if (string.IsNullOrWhiteSpace(externalObjectId))
            throw new ArgumentException("ExternalObjectId requerido.", nameof(externalObjectId));

        TenantId = tenantId;
        CustomerId = customerId;
        LoyaltyCardId = loyaltyCardId;
        Provider = provider;
        ExternalClassId = externalClassId.Trim();
        ExternalObjectId = externalObjectId.Trim();
        Status = DigitalWalletStatus.Created;
        CreatedAt = createdAtUtc;
        UpdatedAt = createdAtUtc;
    }

    public void UpdateExternalIds(string externalClassId, string externalObjectId, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(externalClassId))
            throw new ArgumentException("ExternalClassId requerido.", nameof(externalClassId));
        if (string.IsNullOrWhiteSpace(externalObjectId))
            throw new ArgumentException("ExternalObjectId requerido.", nameof(externalObjectId));

        ExternalClassId = externalClassId.Trim();
        ExternalObjectId = externalObjectId.Trim();
        UpdatedAt = nowUtc;
    }

    public void MarkSynchronized(DateTime nowUtc)
    {
        Status = DigitalWalletStatus.Active;
        LastSynchronizedAt = nowUtc;
        LastSynchronizationError = null;
        UpdatedAt = nowUtc;
    }

    public void MarkSynchronizationFailed(string error, DateTime nowUtc)
    {
        Status = DigitalWalletStatus.Error;
        LastSynchronizationError = string.IsNullOrWhiteSpace(error)
            ? "Google Wallet synchronization failed."
            : error.Trim();
        UpdatedAt = nowUtc;
    }

    public void RecordSaveLinkCreated(DateTime nowUtc)
    {
        LastSaveLinkCreatedAt = nowUtc;
        UpdatedAt = nowUtc;
    }

    public void MarkInactive(DateTime nowUtc)
    {
        Status = DigitalWalletStatus.Inactive;
        UpdatedAt = nowUtc;
    }
}

