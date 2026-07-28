using LoyaltyCloud.Application.Common.Wallet;
using LoyaltyCloud.Common.Results;

namespace LoyaltyCloud.Application.Common.Interfaces;

public interface IMemberWalletDataService
{
    Task<Result<MemberWalletData>> GetBySerialNumberAsync(string serialNumber, CancellationToken ct = default);
}

