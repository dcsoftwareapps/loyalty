using LoyaltyCloud.Domain.Enums;

namespace LoyaltyCloud.Application.Common.Interfaces;

public interface ITenantProgramReadService
{
    Task<LoyaltyProgramType> GetCurrentAsync(CancellationToken ct = default);
}
