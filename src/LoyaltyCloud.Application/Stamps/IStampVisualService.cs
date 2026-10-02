using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Application.Common.Interfaces;

namespace LoyaltyCloud.Application.Stamps;

public enum StampImageKind { Empty, Filled, Reward }
public sealed record StampVisualDto(string EmptyPreview, string FilledPreview, IReadOnlyDictionary<Guid, string> RewardPreviews);
public sealed record StampWalletReward(int RequiredStamps, Guid RewardIdentityId, byte[]? Image);
public sealed record StampStripAsset(string Name, byte[] Bytes);
public sealed record StampWalletProjection(Guid CycleId, Guid VersionId, int CurrentStamps, int MaxStamps,
    byte[]? EmptyImage, byte[]? FilledImage, IReadOnlyList<StampWalletReward> Rewards);

public interface IStampVisualService
{
    Task<StampVisualDto> ReadAsync(CancellationToken ct = default);
    Task<Result> UpdateAsync(StampImageKind kind, Guid? rewardIdentityId, byte[]? image, CancellationToken ct = default);
}

public interface IStampVisualAssetStore
{
    Task<string> UploadAsync(Guid tenantId, byte[] png, CancellationToken ct);
    Task<byte[]?> ReadAsync(Guid tenantId, string? blobName, CancellationToken ct);
}

public interface IStampWalletProjectionService
{
    Task<StampWalletProjection> ReadAsync(string serialNumber, CancellationToken ct = default);
    IReadOnlyList<StampStripAsset> Render(StampWalletProjection projection, string backgroundColor);
}
