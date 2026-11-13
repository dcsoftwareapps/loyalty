using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Application.Common.Interfaces;

namespace LoyaltyCloud.Application.Stamps;

public enum StampImageKind { Empty, Filled, Reward, UpcomingReward }
public sealed record StampUpcomingRewardPreview(string Preview, bool IsCustom);
public sealed record StampVisualDto(string EmptyPreview, string FilledPreview, IReadOnlyDictionary<Guid, string> RewardPreviews)
{
    public IReadOnlyDictionary<Guid, StampUpcomingRewardPreview> UpcomingRewardPreviews { get; init; }
        = new Dictionary<Guid, StampUpcomingRewardPreview>();
}
public enum StampWalletRewardState { Upcoming, Available, Redeemed }
public sealed record StampWalletReward(int RequiredStamps, Guid RewardIdentityId, byte[]? Image,
    StampWalletRewardState? State = null, byte[]? UpcomingImage = null);
public sealed record StampWalletEvent(Guid TransactionId, string Operation, bool IsRollover,
    string Value, string ChangeMessage, bool RewardUnlocked);
public sealed record StampWalletEarnedReward(Guid Id, string Name, string ShortName,
    int RequiredStamps, StampWalletRewardState State, int CycleSequence);
public sealed record StampStripAsset(string Name, byte[] Bytes);
public sealed record StampWalletProjection(Guid CycleId, Guid VersionId, int CurrentStamps, int MaxStamps,
    byte[]? EmptyImage, byte[]? FilledImage, IReadOnlyList<StampWalletReward> Rewards,
    StampWalletEvent? Event = null, int CycleSequence = 1,
    IReadOnlyList<StampWalletEarnedReward>? EarnedRewards = null);

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
