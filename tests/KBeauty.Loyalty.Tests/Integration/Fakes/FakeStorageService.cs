using KBeauty.Loyalty.Application.Common.Interfaces;

namespace KBeauty.Loyalty.Tests.Integration.Fakes;

/// <summary>Storage en memoria — sin Azure Blob ni Azurite.</summary>
public sealed class FakeStorageService : IStorageService
{
    private readonly Dictionary<string, byte[]> _store = new(StringComparer.OrdinalIgnoreCase);

    public Task<string> UploadPassAsync(string serialNumber, byte[] passBytes, CancellationToken ct = default)
    {
        _store[serialNumber] = passBytes;
        return Task.FromResult($"https://test.local/passes/{serialNumber}.pkpass");
    }

    public Task<byte[]?> DownloadPassAsync(string serialNumber, CancellationToken ct = default) =>
        Task.FromResult(_store.GetValueOrDefault(serialNumber));
}
