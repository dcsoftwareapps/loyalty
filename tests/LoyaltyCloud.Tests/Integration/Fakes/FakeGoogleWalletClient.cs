using LoyaltyCloud.Infrastructure.Services.GoogleWallet;

namespace LoyaltyCloud.Tests.Integration.Fakes;

public sealed class FakeGoogleWalletClient : IGoogleWalletClient
{
    public List<GoogleWalletClassData> Classes { get; } = new();
    public List<GoogleWalletObjectData> Objects { get; } = new();
    public List<GoogleWalletMessageCall> Messages { get; } = new();
    public string? FailingObjectId { get; set; }

    public Task EnsureLoyaltyClassAsync(GoogleWalletClassData walletClass, CancellationToken ct = default)
    {
        Classes.Add(walletClass);
        return Task.CompletedTask;
    }

    public Task CreateOrUpdateObjectAsync(GoogleWalletObjectData walletObject, CancellationToken ct = default)
    {
        Objects.Add(walletObject);
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

