using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Entities;

namespace LoyaltyCloud.Tests.Integration.Fakes;

/// <summary>Generator que no firma nada — útil para tests sin certificados.</summary>
internal sealed class FakePassGeneratorService : IPassGeneratorService
{
    public Task<byte[]> GeneratePassAsync(LoyaltyCard card, Customer customer, CancellationToken ct = default) =>
        Task.FromResult(new byte[] { 0x50, 0x4B });  // dummy "PK" (zip magic)

    public Task<string> GetPassDownloadUrlAsync(string serialNumber, CancellationToken ct = default) =>
        Task.FromResult($"https://test.local/passes/{serialNumber}.pkpass");
}
