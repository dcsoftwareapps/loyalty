using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Services.GoogleWallet;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LoyaltyCloud.Tests.Infrastructure;

public sealed class LoyaltyBrandingSyncTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Refresh_OnlyCurrentTenant_StableIds_NoObjectWrites(bool providerFails)
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        foreach (var id in new[] { a, b })
        {
            await using var seed = Context(options, id);
            seed.MemberDigitalWallets.Add(new MemberDigitalWallet(Guid.NewGuid(), id, Guid.NewGuid(), Guid.NewGuid(),
                DigitalWalletProvider.Google, "issuer." + id.ToString("N"), "issuer.object_" + id.ToString("N"), DateTime.UtcNow));
            await seed.SaveChangesAsync();
        }
        var reader = new Mock<ITenantWalletBrandingReadService>();
        reader.Setup(x => x.GetForTenantAsync(a, It.IsAny<CancellationToken>())).ReturnsAsync(new TenantWalletBrandingDto(
            a, "a", "Tenant A", "Tenant A", "Loyalty", "", "", "", "#123456", null, null, 100,
            "Image", "cover.png", "", "", false, false));
        var client = new Mock<IGoogleWalletClient>();
        if (providerFails) client.Setup(x => x.EnsureLoyaltyClassAsync(It.IsAny<GoogleWalletClassData>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("test provider failure"));
        var service = new GoogleWalletBrandingSynchronizer(new Factory(() => Context(options, a)), Tenant(a).Object,
            reader.Object, client.Object, new GoogleWalletObjectMapper(),
            Options.Create(new GoogleWalletOptions { Enabled = true, LogoUri = "https://assets.example/logo.png" }),
            NullLogger<GoogleWalletBrandingSynchronizer>.Instance);
        Assert.Equal(!providerFails, await service.RefreshAsync(a, default));
        Assert.Equal(!providerFails, await service.RefreshAsync(a, default));
        client.Verify(x => x.EnsureLoyaltyClassAsync(It.Is<GoogleWalletClassData>(c =>
            c.Id == "issuer." + a.ToString("N") && c.HexBackgroundColor == "#123456"), It.IsAny<CancellationToken>()), Times.Exactly(2));
        client.Verify(x => x.CreateOrUpdateObjectAsync(It.IsAny<GoogleWalletObjectData>(), It.IsAny<CancellationToken>()), Times.Never);
        reader.Verify(x => x.GetForTenantAsync(b, It.IsAny<CancellationToken>()), Times.Never);
        await using var verify = Context(options, a);
        var mapping = await verify.MemberDigitalWallets.SingleAsync();
        Assert.Equal("issuer.object_" + a.ToString("N"), mapping.ExternalObjectId);
        Assert.Equal(2, await verify.MemberDigitalWallets.IgnoreQueryFilters().CountAsync());
    }
    private static Mock<ITenantContext> Tenant(Guid id)
    {
        var mock = new Mock<ITenantContext>(); mock.SetupGet(x => x.TenantId).Returns(id); mock.SetupGet(x => x.HasTenant).Returns(true); return mock;
    }
    private static AppDbContext Context(DbContextOptions<AppDbContext> options, Guid id) => new(options, new Mock<IPublisher>().Object, Tenant(id).Object);
    private sealed class Factory(Func<AppDbContext> create) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => create();
    }
}
