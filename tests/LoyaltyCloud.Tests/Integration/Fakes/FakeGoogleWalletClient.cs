using LoyaltyCloud.Infrastructure.Services.GoogleWallet;

namespace LoyaltyCloud.Tests.Integration.Fakes;

public sealed class FakeGoogleWalletClient : IGoogleWalletClient
{
    public List<GoogleWalletClassData> Classes { get; } = new();
    public List<GoogleWalletObjectData> Objects { get; } = new();
    public List<bool> ObjectUpdateNotifications { get; } = new();
    public List<GoogleStampWalletClassData> StampClasses { get; } = new();
    public List<GoogleStampWalletObjectData> StampObjects { get; } = new();
    public List<bool> StampObjectUpdateNotifications { get; } = new();
    public List<GoogleGiftCardClassData> GiftCardClasses { get; } = new();
    public List<GoogleGiftCardObjectData> GiftCardObjects { get; } = new();
    public List<GoogleWalletMessageCall> Messages { get; } = new();
    public string? FailingObjectId { get; set; }
    public Func<GoogleStampWalletObjectData, Task>? BeforeStampObjectUpdateAsync { get; set; }

    public Task EnsureLoyaltyClassAsync(GoogleWalletClassData walletClass, CancellationToken ct = default)
    {
        Classes.Add(walletClass);
        return Task.CompletedTask;
    }

    public Task CreateOrUpdateObjectAsync(GoogleWalletObjectData walletObject, bool notifyOnUpdate = false, CancellationToken ct = default)
    {
        Objects.Add(walletObject);
        ObjectUpdateNotifications.Add(notifyOnUpdate);
        return Task.CompletedTask;
    }

    public Task EnsureStampLoyaltyClassAsync(GoogleStampWalletClassData walletClass, CancellationToken ct = default)
    {
        StampClasses.Add(walletClass);
        return Task.CompletedTask;
    }

    public async Task CreateOrUpdateStampObjectAsync(GoogleStampWalletObjectData walletObject, bool notifyOnUpdate = false, CancellationToken ct = default)
    {
        if (string.Equals(walletObject.Id, FailingObjectId, StringComparison.Ordinal))
            throw new InvalidOperationException("Google Wallet object unavailable.");
        if (BeforeStampObjectUpdateAsync is not null)
            await BeforeStampObjectUpdateAsync(walletObject);
        StampObjects.Add(walletObject);
        StampObjectUpdateNotifications.Add(notifyOnUpdate);
    }

    public Task EnsureGiftCardClassAsync(GoogleGiftCardClassData walletClass, CancellationToken ct = default)
    {
        GiftCardClasses.Add(walletClass);
        return Task.CompletedTask;
    }

    public Task CreateOrUpdateGiftCardObjectAsync(GoogleGiftCardObjectData walletObject, CancellationToken ct = default)
    {
        GiftCardObjects.Add(walletObject);
        return Task.CompletedTask;
    }

    public Task AddMessageAsync(string objectId, string header, string body, string messageId, CancellationToken ct = default)
    {
        if (string.Equals(objectId, FailingObjectId, StringComparison.Ordinal))
            throw new InvalidOperationException("Google Wallet object unavailable.");
        Messages.Add(new GoogleWalletMessageCall(objectId, header, body, messageId));
        return Task.CompletedTask;
    }

    public sealed record GoogleWalletMessageCall(string ObjectId, string Header, string Body, string MessageId);
}
