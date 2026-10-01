using LoyaltyCloud.Common.Results;
using MediatR;

namespace LoyaltyCloud.Application.Stamps;

public sealed record StampRewardDto(Guid Id, string Name, string? Description, int RequiredStamps, DateTime EarnedAt);
public sealed record StampStateDto(string SerialNumber, string CustomerName, Guid CycleId, int CycleSequence,
    int CurrentStamps, int MaxStamps, bool CycleCompleted, IReadOnlyList<StampRewardDto> AvailableRewards);
public sealed record StampOperationResult(StampStateDto State, IReadOnlyList<StampRewardDto> UnlockedRewards);
public sealed record EarnStampCommand(string SerialNumber, string IdempotencyKey) : IRequest<Result<StampOperationResult>>;
public sealed record ReverseLastStampCommand(string SerialNumber, string IdempotencyKey) : IRequest<Result<StampOperationResult>>;
public sealed record RedeemStampRewardCommand(string SerialNumber, Guid RewardId, string IdempotencyKey) : IRequest<Result<StampOperationResult>>;
public sealed record GetStampStateQuery(string SerialNumber) : IRequest<Result<StampStateDto>>;

public interface IStampOperationsService
{
    Task<Result<StampStateDto>> ReadAsync(string serialNumber, CancellationToken ct = default);
    Task<Result<StampOperationResult>> ExecuteAsync(string operation, string serialNumber,
        string idempotencyKey, Guid? rewardId = null, CancellationToken ct = default);
}

public sealed class EarnStampHandler(IStampOperationsService service) : IRequestHandler<EarnStampCommand, Result<StampOperationResult>>
{
    public Task<Result<StampOperationResult>> Handle(EarnStampCommand r, CancellationToken ct) => service.ExecuteAsync("Earn", r.SerialNumber, r.IdempotencyKey, ct: ct);
}
public sealed class ReverseLastStampHandler(IStampOperationsService service) : IRequestHandler<ReverseLastStampCommand, Result<StampOperationResult>>
{
    public Task<Result<StampOperationResult>> Handle(ReverseLastStampCommand r, CancellationToken ct) => service.ExecuteAsync("Reversal", r.SerialNumber, r.IdempotencyKey, ct: ct);
}
public sealed class RedeemStampRewardHandler(IStampOperationsService service) : IRequestHandler<RedeemStampRewardCommand, Result<StampOperationResult>>
{
    public Task<Result<StampOperationResult>> Handle(RedeemStampRewardCommand r, CancellationToken ct) => service.ExecuteAsync("Redeem", r.SerialNumber, r.IdempotencyKey, r.RewardId, ct);
}
public sealed class GetStampStateHandler(IStampOperationsService service) : IRequestHandler<GetStampStateQuery, Result<StampStateDto>>
{
    public Task<Result<StampStateDto>> Handle(GetStampStateQuery r, CancellationToken ct) => service.ReadAsync(r.SerialNumber, ct);
}
