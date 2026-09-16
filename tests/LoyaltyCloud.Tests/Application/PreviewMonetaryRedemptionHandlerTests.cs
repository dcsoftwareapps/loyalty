using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Redemptions.Queries.PreviewMonetaryRedemption;
using LoyaltyCloud.Common.Constants;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.Repositories;
using Moq;
using Xunit;
using static LoyaltyCloud.Tests.Application.HandlerTestHelpers;

namespace LoyaltyCloud.Tests.Application;

public sealed class PreviewMonetaryRedemptionHandlerTests
{
    [Fact]
    [Trait("Category", "MonetaryRedemption")]
    public async Task Handle_ShouldCalculateDiscountWithoutMutatingPoints()
    {
        var card = CardWith(250);
        var handler = BuildHandler(card, availableLotPoints: 250, rate: 10m);

        var result = await handler.Handle(
            new PreviewMonetaryRedemptionQuery(card.SerialNumber, 100),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(card.SerialNumber, result.Value.SerialNumber);
        Assert.Equal(100, result.Value.PointsToRedeem);
        Assert.Equal(10m, result.Value.MonetaryAmount);
        Assert.Equal("MXN", result.Value.MonetaryCurrency);
        Assert.Equal(10m, result.Value.MonetaryPointsPerPesoUnit);
        Assert.Equal(250, result.Value.CurrentPoints);
        Assert.Equal(150, result.Value.RemainingPoints);
        Assert.Equal(250, card.CurrentPoints);
    }

    [Fact]
    [Trait("Category", "MonetaryRedemption")]
    public async Task Handle_ShouldRejectInsufficientUnexpiredLots()
    {
        var card = CardWith(250);
        var handler = BuildHandler(card, availableLotPoints: 50, rate: 10m);

        var result = await handler.Handle(
            new PreviewMonetaryRedemptionQuery(card.SerialNumber, 100),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Saldo disponible insuficiente", result.Error);
        Assert.Equal(250, card.CurrentPoints);
    }

    private static PreviewMonetaryRedemptionHandler BuildHandler(
        LoyaltyCard card,
        int availableLotPoints,
        decimal rate)
    {
        var cards = new Mock<ILoyaltyCardRepository>();
        cards.Setup(r => r.GetBySerialNumberAsync(card.SerialNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(card);

        var config = new Mock<IProgramConfigRepository>();
        config.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ProgramConfig(
                    Guid.NewGuid(),
                    card.TenantId,
                    LoyaltyConstants.ConfigKeys.PointsPerPesoUnit,
                    rate.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Now,
                    "test",
                    "test")
            });

        var pointLots = new Mock<IPointLotRepository>();
        pointLots.Setup(r => r.GetAvailableLotsAsync(card.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new PointLot(Guid.NewGuid(), card.TenantId, card.Id, Guid.NewGuid(), availableLotPoints, Now, Now.AddMonths(12), Now)
            });

        return new PreviewMonetaryRedemptionHandler(
            cards.Object,
            config.Object,
            pointLots.Object,
            TenantContext().Object,
            Clock().Object);
    }

    private static LoyaltyCard CardWith(int points)
    {
        var card = new LoyaltyCard(Guid.NewGuid(), KBeautyTenantId, Guid.NewGuid(), "KB-TEST001", Now);
        var snapshot = new LoyaltyCloud.Domain.ValueObjects.ProgramConfigSnapshot(
            10m, 50, 150, 2, true, 12, 0, 1000, 3000, 500, 300, 500, 400, 700, 800, 1200);
        card.EarnPoints(points, TransactionType.Purchase, snapshot, Clock().Object);
        card.ClearDomainEvents();
        return card;
    }
}
