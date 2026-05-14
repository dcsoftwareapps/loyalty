using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Application.Redemptions.Commands.RedeemReward;
using KBeauty.Loyalty.Common.Constants;
using KBeauty.Loyalty.Common.Services;
using KBeauty.Loyalty.Domain.Entities;
using KBeauty.Loyalty.Domain.Enums;
using KBeauty.Loyalty.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using static KBeauty.Loyalty.Tests.Application.HandlerTestHelpers;

namespace KBeauty.Loyalty.Tests.Application;

public class RedeemRewardHandlerTests
{
    private static RedeemRewardHandler BuildHandler(
        LoyaltyCard card,
        RewardCatalogItem reward,
        out Mock<IRedemptionRepository> redemptions)
    {
        var cards = new Mock<ILoyaltyCardRepository>();
        cards.Setup(r => r.GetBySerialNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(card);

        var rewards = new Mock<IRewardCatalogRepository>();
        rewards.Setup(r => r.GetByIdAsync(reward.Id, It.IsAny<CancellationToken>()))
               .ReturnsAsync(reward);

        redemptions = new Mock<IRedemptionRepository>();
        var transactions = new Mock<IPointTransactionRepository>();
        var config = ConfigRepoWithDefaults();

        var devices = new Mock<IDeviceRegistrationRepository>();
        devices.Setup(r => r.GetBySerialNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(Array.Empty<DeviceRegistration>());

        var apn = new Mock<IApnService>();
        var publisher = new Mock<IPublisher>();
        var clock = Clock();
        var uow = NoOpUnitOfWork();

        return new RedeemRewardHandler(
            cards.Object, rewards.Object, redemptions.Object, transactions.Object,
            config.Object, devices.Object, apn.Object, publisher.Object,
            clock.Object, uow.Object,
            NullLogger<RedeemRewardHandler>.Instance);
    }

    /// <summary>Crea un card con saldo y nivel dados mediante EarnPoints.</summary>
    private static LoyaltyCard CardWith(int points, IDateTimeProvider dt)
    {
        var card = new LoyaltyCard(Guid.NewGuid(), Guid.NewGuid(), "KB-TEST001", Now);
        if (points > 0)
        {
            var snapshot = new KBeauty.Loyalty.Domain.ValueObjects.ProgramConfigSnapshot(
                10m, 50, 150, 2, 0, 1000, 3000, 500, 300, 500, 400, 700, 800, 1200);
            card.EarnPoints(points, TransactionType.Purchase, snapshot, dt);
            card.ClearDomainEvents();
        }
        return card;
    }

    private static RewardCatalogItem NewReward(int cost, string minLevel) =>
        new(Guid.NewGuid(), $"Reward {cost}pts", "Test reward", cost, minLevel);

    // =========================================================================

    [Fact]
    public async Task Handle_ShouldFail_WhenLevelNotEligible()
    {
        // Card en Mist con saldo alto pero reward exige Radiance.
        var dt = Clock().Object;
        var card = CardWith(2500, dt); // → Glow (no Radiance)
        var reward = NewReward(1200, LoyaltyConstants.Levels.Radiance);
        var handler = BuildHandler(card, reward, out var redemptions);

        var result = await handler.Handle(
            new RedeemRewardCommand("KB-TEST001", reward.Id, "test"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("nivel", result.Error, StringComparison.OrdinalIgnoreCase);
        redemptions.Verify(r => r.AddAsync(It.IsAny<Redemption>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenInsufficientPoints()
    {
        // Mist con 100 pts intentando canjear mini-product (300 pts).
        var dt = Clock().Object;
        var card = CardWith(100, dt);
        var reward = NewReward(300, LoyaltyConstants.Levels.Mist);
        var handler = BuildHandler(card, reward, out var redemptions);

        var result = await handler.Handle(
            new RedeemRewardCommand("KB-TEST001", reward.Id, "test"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("insuficiente", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(100, card.CurrentPoints); // no se descontó
        redemptions.Verify(r => r.AddAsync(It.IsAny<Redemption>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldCreatePendingRedemption_WhenValid()
    {
        var dt = Clock().Object;
        var card = CardWith(500, dt);
        var reward = NewReward(300, LoyaltyConstants.Levels.Mist);
        var handler = BuildHandler(card, reward, out var redemptions);

        Redemption? captured = null;
        redemptions.Setup(r => r.AddAsync(It.IsAny<Redemption>(), It.IsAny<CancellationToken>()))
                   .Callback<Redemption, CancellationToken>((r, _) => captured = r);

        var result = await handler.Handle(
            new RedeemRewardCommand("KB-TEST001", reward.Id, "test"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(reward.Name, result.Value.RewardName);
        Assert.Equal(300, result.Value.PointsSpent);
        Assert.Equal(200, result.Value.RemainingPoints);
        Assert.Equal(RedemptionStatus.Pending, result.Value.Status);

        Assert.NotNull(captured);
        Assert.Equal(RedemptionStatus.Pending, captured!.Status);
        Assert.Equal(card.Id, captured.LoyaltyCardId);
        Assert.Equal(reward.Id, captured.RewardCatalogItemId);

        // El saldo de la card bajó.
        Assert.Equal(200, card.CurrentPoints);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenRewardNotFound()
    {
        var dt = Clock().Object;
        var card = CardWith(500, dt);

        var cards = new Mock<ILoyaltyCardRepository>();
        cards.Setup(r => r.GetBySerialNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(card);

        var rewards = new Mock<IRewardCatalogRepository>();
        rewards.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((RewardCatalogItem?)null);

        var handler = new RedeemRewardHandler(
            cards.Object, rewards.Object,
            new Mock<IRedemptionRepository>().Object,
            new Mock<IPointTransactionRepository>().Object,
            ConfigRepoWithDefaults().Object,
            new Mock<IDeviceRegistrationRepository>().Object,
            new Mock<IApnService>().Object,
            new Mock<IPublisher>().Object,
            Clock().Object,
            NoOpUnitOfWork().Object,
            NullLogger<RedeemRewardHandler>.Instance);

        var result = await handler.Handle(
            new RedeemRewardCommand("KB-TEST001", Guid.NewGuid(), "test"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("no encontrado", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}
