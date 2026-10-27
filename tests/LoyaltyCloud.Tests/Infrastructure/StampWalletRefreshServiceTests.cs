using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace LoyaltyCloud.Tests.Infrastructure;

public sealed class StampWalletRefreshServiceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Providers_are_independent_and_both_are_attempted(bool appleFails, bool googleFails)
    {
        var apple = new Mock<IAppleWalletPassRefreshService>();
        var google = new Mock<IGoogleStampWalletService>();
        if (appleFails) apple.Setup(x => x.RefreshCardAsync(It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<PassUpdateReason>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("apple"));
        if (googleFails) google.Setup(x => x.SynchronizeBySerialNumberIfExistsAsync("LC-1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("google"));
        var service = new StampWalletRefreshService(apple.Object, google.Object,
            NullLogger<StampWalletRefreshService>.Instance);

        await service.RefreshCardAsync(Guid.NewGuid(), Guid.NewGuid(), "LC-1", PassUpdateReason.StampEarned);

        apple.Verify(x => x.RefreshCardAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), PassUpdateReason.StampEarned,
            It.IsAny<CancellationToken>()), Times.Once);
        google.Verify(x => x.SynchronizeBySerialNumberIfExistsAsync("LC-1", It.IsAny<CancellationToken>()), Times.Once);
    }
}
