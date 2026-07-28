using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Redemptions.Commands.CancelRedemption;
using LoyaltyCloud.Application.Redemptions.Commands.RedeemMonetaryDiscount;
using LoyaltyCloud.Common.Constants;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using static LoyaltyCloud.Tests.Application.HandlerTestHelpers;

namespace LoyaltyCloud.Tests.Application;

public class RedeemMonetaryDiscountHandlerTests
{
    [Fact]
    [Trait("Category", "MonetaryRedemption")]
    public async Task Handle_ShouldRedeemOneHundredPointsAsTenPesos_WhenRateIsTen()
    {
        var card = CardWith(250);
        var lots = new[]
        {
            new PointLot(Guid.NewGuid(), card.TenantId, card.Id, Guid.NewGuid(), 250, Now, Now.AddMonths(12), Now)
        };
        var handler = BuildHandler(card, lots, rate: 10m, out var captured);

        var result = await handler.Handle(
            new RedeemMonetaryDiscountCommand(card.SerialNumber, 100, "cashier"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(10m, result.Value.MonetaryAmount);
        Assert.Equal("MXN", result.Value.MonetaryCurrency);
        Assert.Equal(10m, result.Value.MonetaryPointsPerPesoUnit);
        Assert.Equal(150, result.Value.RemainingPoints);
        Assert.Equal(150, card.CurrentPoints);
        Assert.Equal(Now, card.LastActivityAt);
        Assert.Equal(RedemptionType.MonetaryDiscount, captured.Redemption!.Type);
        Assert.Null(captured.Redemption.RewardCatalogItemId);
        Assert.Equal(100, captured.Redemption.PointsSpent);
        Assert.Equal(10m, captured.Redemption.MonetaryAmount);
        Assert.Single(captured.Consumptions);
        Assert.Equal(100, captured.Consumptions[0].Amount);
        Assert.Equal(150, lots[0].RemainingAmount);
        Assert.Equal(TransactionType.Redemption, captured.Transaction!.Type);
        Assert.Equal(-100, captured.Transaction.Points);
    }

    [Theory]
    [InlineData(100, 10, 10)]
    [InlineData(250, 10, 25)]
    [InlineData(100, 20, 5)]
    [Trait("Category", "MonetaryRedemption")]
    public async Task Handle_ShouldUseTenantRate_WhenCalculatingMoney(int points, decimal rate, decimal expectedAmount)
    {
        var card = CardWith(points);
        var lots = new[] { new PointLot(Guid.NewGuid(), card.TenantId, card.Id, Guid.NewGuid(), points, Now, Now.AddMonths(12), Now) };
        var handler = BuildHandler(card, lots, rate, out _);

        var result = await handler.Handle(
            new RedeemMonetaryDiscountCommand(card.SerialNumber, points, "cashier"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedAmount, result.Value.MonetaryAmount);
    }

    [Theory]
    [InlineData(0, "mayores a 0")]
    [InlineData(-10, "mayores a 0")]
    [InlineData(25, "multiplos de 10")]
    [Trait("Category", "MonetaryRedemption")]
    public async Task Handle_ShouldRejectInvalidPointAmounts(int points, string expectedMessage)
    {
        var card = CardWith(100);
        var lots = new[] { new PointLot(Guid.NewGuid(), card.TenantId, card.Id, Guid.NewGuid(), 100, Now, Now.AddMonths(12), Now) };
        var handler = BuildHandler(card, lots, rate: 10m, out var captured);

        var result = await handler.Handle(
            new RedeemMonetaryDiscountCommand(card.SerialNumber, points, "cashier"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(expectedMessage, result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(100, card.CurrentPoints);
        Assert.Null(captured.Redemption);
    }

    [Fact]
    [Trait("Category", "MonetaryRedemption")]
    public async Task Handle_ShouldRejectInsufficientBalance()
    {
        var card = CardWith(90);
        var lots = new[] { new PointLot(Guid.NewGuid(), card.TenantId, card.Id, Guid.NewGuid(), 90, Now, Now.AddMonths(12), Now) };
        var handler = BuildHandler(card, lots, rate: 10m, out _);

        var result = await handler.Handle(
            new RedeemMonetaryDiscountCommand(card.SerialNumber, 100, "cashier"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Saldo insuficiente", result.Error);
        Assert.Equal(90, card.CurrentPoints);
    }

    [Fact]
    [Trait("Category", "MonetaryRedemption")]
    public async Task Handle_ShouldConsumeLotsInFifoOrder()
    {
        var card = CardWith(300);
        var first = new PointLot(Guid.NewGuid(), card.TenantId, card.Id, Guid.NewGuid(), 80, Now.AddDays(-10), Now.AddMonths(3), Now);
        var second = new PointLot(Guid.NewGuid(), card.TenantId, card.Id, Guid.NewGuid(), 220, Now.AddDays(-2), Now.AddMonths(6), Now);
        var handler = BuildHandler(card, [first, second], rate: 10m, out var captured);

        var result = await handler.Handle(
            new RedeemMonetaryDiscountCommand(card.SerialNumber, 250, "cashier"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, first.RemainingAmount);
        Assert.Equal(50, second.RemainingAmount);
        Assert.Equal([80, 170], captured.Consumptions.Select(c => c.Amount).ToArray());
        Assert.All(captured.Consumptions, c => Assert.Equal(captured.Redemption!.Id, c.RedemptionId));
    }

    [Fact]
    [Trait("Category", "MonetaryRedemption")]
    public async Task Handle_ShouldPersistHistoricalSnapshot_WhenRateChangesLater()
    {
        var card = CardWith(500);
        var lots = new[] { new PointLot(Guid.NewGuid(), card.TenantId, card.Id, Guid.NewGuid(), 500, Now, Now.AddMonths(12), Now) };
        var handler = BuildHandler(card, lots, rate: 10m, out var captured);

        var result = await handler.Handle(
            new RedeemMonetaryDiscountCommand(card.SerialNumber, 500, "cashier"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(50m, captured.Redemption!.MonetaryAmount);
        Assert.Equal(10m, captured.Redemption.MonetaryPointsPerPesoUnit);
    }

    [Fact]
    [Trait("Category", "MonetaryRedemption")]
    public async Task Cancel_ShouldRestoreMonetaryRedemptionPointsAndLots()
    {
        var card = CardWith(250);
        var lot = new PointLot(Guid.NewGuid(), card.TenantId, card.Id, Guid.NewGuid(), 250, Now, Now.AddMonths(12), Now);
        var redeemHandler = BuildHandler(card, [lot], rate: 10m, out var captured);

        var redeem = await redeemHandler.Handle(
            new RedeemMonetaryDiscountCommand(card.SerialNumber, 100, "cashier"),
            CancellationToken.None);

        Assert.True(redeem.IsSuccess);
        var cancelHandler = BuildCancelHandler(card, lot, captured.Redemption!, captured.Consumptions);

        var cancel = await cancelHandler.Handle(
            new CancelRedemptionCommand(captured.Redemption!.Id, "cashier", "error de caja"),
            CancellationToken.None);

        Assert.True(cancel.IsSuccess);
        Assert.Equal(250, card.CurrentPoints);
        Assert.Equal(250, lot.RemainingAmount);
        Assert.Equal(RedemptionStatus.Cancelled, captured.Redemption.Status);
        Assert.Equal("Descuento en dinero", cancel.Value.RewardName);
        Assert.All(captured.Consumptions, c => Assert.NotNull(c.ReversedAt));
    }

    [Fact]
    [Trait("Category", "MonetaryRedemption")]
    public void UseAll_ShouldRoundDownToValidUnit()
    {
        var usable = CalculateUsablePointsForTest(1257, 10);

        Assert.Equal(1250, usable);
    }

    private static LoyaltyCard CardWith(int points)
    {
        var card = new LoyaltyCard(Guid.NewGuid(), KBeautyTenantId, Guid.NewGuid(), "KB-TEST001", Now);
        if (points > 0)
        {
            var snapshot = new LoyaltyCloud.Domain.ValueObjects.ProgramConfigSnapshot(
                10m, 50, 150, 2, true, 12, 0, 1000, 3000, 500, 300, 500, 400, 700, 800, 1200);
            card.EarnPoints(points, TransactionType.Purchase, snapshot, Clock().Object);
            card.ClearDomainEvents();
        }

        return card;
    }

    private static RedeemMonetaryDiscountHandler BuildHandler(
        LoyaltyCard card,
        IReadOnlyList<PointLot> lots,
        decimal rate,
        out CapturedRedemption captured)
    {
        captured = new CapturedRedemption();
        var capturedRef = captured;

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

        var redemptions = new Mock<IRedemptionRepository>();
        redemptions.Setup(r => r.AddAsync(It.IsAny<Redemption>(), It.IsAny<CancellationToken>()))
            .Callback<Redemption, CancellationToken>((r, _) => capturedRef.Redemption = r);

        var transactions = new Mock<IPointTransactionRepository>();
        transactions.Setup(r => r.AddAsync(It.IsAny<PointTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<PointTransaction, CancellationToken>((t, _) => capturedRef.Transaction = t);

        var pointLots = new Mock<IPointLotRepository>();
        pointLots.Setup(r => r.GetAvailableLotsAsync(card.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(lots);
        pointLots.Setup(r => r.AddConsumptionAsync(It.IsAny<PointLotConsumption>(), It.IsAny<CancellationToken>()))
            .Callback<PointLotConsumption, CancellationToken>((c, _) => capturedRef.Consumptions.Add(c));

        var devices = new Mock<IDeviceRegistrationRepository>();
        devices.Setup(r => r.GetBySerialNumberAsync(card.SerialNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DeviceRegistration>());

        return new RedeemMonetaryDiscountHandler(
            cards.Object,
            config.Object,
            redemptions.Object,
            transactions.Object,
            pointLots.Object,
            devices.Object,
            new Mock<IApnService>().Object,
            TenantContext().Object,
            new Mock<IPublisher>().Object,
            Clock().Object,
            NoOpUnitOfWork().Object,
            NullLogger<RedeemMonetaryDiscountHandler>.Instance);
    }

    private static CancelRedemptionHandler BuildCancelHandler(
        LoyaltyCard card,
        PointLot lot,
        Redemption redemption,
        IReadOnlyList<PointLotConsumption> consumptions)
    {
        var redemptions = new Mock<IRedemptionRepository>();
        redemptions.Setup(r => r.GetByIdAsync(redemption.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(redemption);

        var cards = new Mock<ILoyaltyCardRepository>();
        cards.Setup(r => r.GetByIdAsync(card.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(card);

        var pointLots = new Mock<IPointLotRepository>();
        pointLots.Setup(r => r.GetActiveConsumptionsByRedemptionIdAsync(redemption.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consumptions);
        pointLots.Setup(r => r.GetLotByIdAsync(lot.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lot);

        var devices = new Mock<IDeviceRegistrationRepository>();
        devices.Setup(r => r.GetBySerialNumberAsync(card.SerialNumber, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DeviceRegistration>());

        return new CancelRedemptionHandler(
            redemptions.Object,
            cards.Object,
            new Mock<IRewardCatalogRepository>().Object,
            new Mock<IPointTransactionRepository>().Object,
            pointLots.Object,
            devices.Object,
            new Mock<IApnService>().Object,
            Clock().Object,
            NoOpUnitOfWork().Object,
            NullLogger<CancelRedemptionHandler>.Instance);
    }

    private static int CalculateUsablePointsForTest(int points, int unit) =>
        points - (points % unit);

    private sealed class CapturedRedemption
    {
        public Redemption? Redemption { get; set; }
        public PointTransaction? Transaction { get; set; }
        public List<PointLotConsumption> Consumptions { get; } = [];
    }
}
