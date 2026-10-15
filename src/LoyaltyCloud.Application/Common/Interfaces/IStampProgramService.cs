using LoyaltyCloud.Application.Stamps;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Domain.Entities;

namespace LoyaltyCloud.Application.Common.Interfaces;

public interface IStampProgramService
{
    Task<StampProgramVersion?> GetActiveAsync(CancellationToken ct = default);
    Task<Result<StampProgramDto>> CreateVersionAsync(SetStampProgramCommand command, CancellationToken ct = default);
    Task<StampProgramDto?> ReadActiveAsync(CancellationToken ct = default);
    void AddInitialCycle(LoyaltyCard card, StampProgramVersion program, DateTime nowUtc);
}
