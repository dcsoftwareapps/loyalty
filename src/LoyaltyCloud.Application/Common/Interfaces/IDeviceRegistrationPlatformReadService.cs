namespace LoyaltyCloud.Application.Common.Interfaces;

public interface IDeviceRegistrationPlatformReadService
{
    Task<WalletUpdatableSerialsResult> GetUpdatableSerialsAsync(
        string deviceLibraryIdentifier,
        string passTypeIdentifier,
        DateTime? passesUpdatedSince,
        CancellationToken cancellationToken = default);
}

public sealed record WalletUpdatableSerialsResult(
    IReadOnlyList<string> SerialNumbers,
    DateTime LastUpdated);

