using LoyaltyCloud.Domain.Common;

namespace LoyaltyCloud.Domain.Entities;

public sealed class StampRewardIdentity : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }
    public string? ImageBlobName { get; private set; }
    public string? UpcomingImageBlobName { get; private set; }
    private StampRewardIdentity() { }
    public StampRewardIdentity(Guid id, Guid tenantId) : base(id)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty) throw new ArgumentException("Reward identity requires a tenant and ID.");
        TenantId = tenantId;
    }
    public void SetImage(string? blobName) => ImageBlobName = blobName;
    public void SetUpcomingImage(string? blobName) => UpcomingImageBlobName = blobName;
}

public sealed class StampVisualConfiguration : Entity, ITenantOwned
{
    public Guid TenantId { get; private set; }
    public string? EmptyStampBlobName { get; private set; }
    public string? FilledStampBlobName { get; private set; }
    private StampVisualConfiguration() { }
    public StampVisualConfiguration(Guid tenantId) : base(Guid.NewGuid())
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Visual configuration requires a tenant.", nameof(tenantId));
        TenantId = tenantId;
    }
    public void SetImage(bool filled, string? blobName)
    {
        if (filled) FilledStampBlobName = blobName;
        else EmptyStampBlobName = blobName;
    }
}
