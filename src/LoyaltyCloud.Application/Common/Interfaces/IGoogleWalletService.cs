using LoyaltyCloud.Application.Common.Wallet;
using LoyaltyCloud.Common.Results;

namespace LoyaltyCloud.Application.Common.Interfaces;

public interface IGoogleWalletService
{
    Task<Result<GoogleWalletSaveLinkResponse>> GetOrCreateSaveLinkAsync(
        string serialNumber,
        CancellationToken ct = default);

    Task SynchronizeBySerialNumberIfExistsAsync(string serialNumber, CancellationToken ct = default);
}

