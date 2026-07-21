using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Points.Commands.AddPoints;
using LoyaltyCloud.Common.Constants;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using static LoyaltyCloud.Tests.Application.HandlerTestHelpers;

namespace LoyaltyCloud.Tests.Application;

public class AddPointsHandlerTests
{
    private record HandlerSetup(
        AddPointsHandler Handler,
        Mock<ILoyaltyCardRepository> Cards,
        Mock<ICustomerRepository> Customers,
        Mock<IPointTransactionRepository> Transactions,
        Mock<IApnService> Apn,
        Mock<IUnitOfWork> Uow);

    private static HandlerSetup BuildHandler(LoyaltyCard? card, Customer? customer, DateTime? now = null)
    {
        var cards = new Mock<ILoyaltyCardRepository>();
        cards.Setup(r => r.GetBySerialNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(card);

        var customers = new Mock<ICustomerRepository>();
        customers.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(customer);

        var transactions = new Mock<IPointTransactionRepository>();
        transactions.Setup(r => r.GetEligibleLevelPointsAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        var pointLots = new Mock<IPointLotRepository>();
        var config = ConfigRepoWithDefaults();

        var devices = new Mock<IDeviceRegistrationRepository>();
        devices.Setup(r => r.GetBySerialNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(Array.Empty<DeviceRegistration>());

        var apn = new Mock<IApnService>();
        var clock = Clock(now);
        var uow = NoOpUnitOfWork();

        var handler = new AddPointsHandler(
            cards.Object,
            customers.Object,
            transactions.Object,
            pointLots.Object,
            config.Object,
            devices.Object,
            apn.Object,
            LevelCalculator().Object,
            TenantContext().Object,
            clock.Object,
            uow.Object,
            NullLogger<AddPointsHandler>.Instance);

        return new HandlerSetup(handler, cards, customers, transactions, apn, uow);
    }

    // =========================================================================

    [Fact]
    public async Task Handle_ShouldAddCorrectPoints_WhenValidPurchase()
    {
        // ratio default = 10 → $250 / 10 = 25 pts (no es mes de cumple).
        var customer = NewCustomer(dob: new DateTime(1990, 1, 1)); // Enero
        var card = NewCard(customer.Id);
        var setup = BuildHandler(card, customer, now: new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));

        var result = await setup.Handler.Handle(
            new AddPointsCommand("KB-TEST001", PurchaseAmount: 250m, OperatorId: "test"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(25, result.Value.PointsAdded);
        Assert.Equal(25, result.Value.NewTotal);
        Assert.False(result.Value.BirthdayBonusApplied);
        Assert.Equal(LoyaltyConstants.Levels.Mist, result.Value.Level);
        Assert.False(result.Value.LeveledUp);
    }

    [Fact]
    public async Task Handle_ShouldApplyDoublePoints_WhenBirthdayMonth()
    {
        // Cliente nació en junio; "now" = 15 junio 2025 → cumple este mes
        var customer = NewCustomer(dob: new DateTime(1990, 6, 5));
        var card = NewCard(customer.Id);
        var setup = BuildHandler(card, customer, now: new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));

        var result = await setup.Handler.Handle(
            new AddPointsCommand("KB-TEST001", PurchaseAmount: 250m, OperatorId: "test"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(50, result.Value.PointsAdded); // 25 base × 2
        Assert.True(result.Value.BirthdayBonusApplied);
    }

    [Fact]
    public async Task Handle_ShouldCallApnService_WhenPointsAdded()
    {
        var customer = NewCustomer();
        var card = NewCard(customer.Id);

        var cards = new Mock<ILoyaltyCardRepository>();
        cards.Setup(r => r.GetBySerialNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(card);

        var customers = new Mock<ICustomerRepository>();
        customers.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(customer);

        var transactions = new Mock<IPointTransactionRepository>();
        transactions.Setup(r => r.GetEligibleLevelPointsAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        var pointLots = new Mock<IPointLotRepository>();
        var config = ConfigRepoWithDefaults();

        // Un dispositivo registrado para esta tarjeta.
        var devices = new Mock<IDeviceRegistrationRepository>();
        devices.Setup(r => r.GetBySerialNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new[]
               {
                   new DeviceRegistration(Guid.NewGuid(), KBeautyTenantId, "device-1",
                       "pass.com.kbeautymx.loyalty", "KB-TEST001", "push-token-abc", Now)
               });

        var apn = new Mock<IApnService>();
        var clock = Clock();
        var uow = NoOpUnitOfWork();

        var handler = new AddPointsHandler(
            cards.Object, customers.Object, transactions.Object, pointLots.Object, config.Object,
            devices.Object, apn.Object, LevelCalculator().Object, TenantContext().Object, clock.Object, uow.Object,
            NullLogger<AddPointsHandler>.Instance);

        await handler.Handle(
            new AddPointsCommand("KB-TEST001", 200m, "test"),
            CancellationToken.None);

        apn.Verify(s => s.SendPassUpdateAsync(
            "push-token-abc",
            It.IsAny<PassUpdateReason>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnFail_WhenSerialNotFound()
    {
        var setup = BuildHandler(card: null, customer: null);

        var result = await setup.Handler.Handle(
            new AddPointsCommand("KB-NOEXIST", 100m, "test"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("No se encontró", result.Error);
        setup.Apn.Verify(s => s.SendPassUpdateAsync(
            It.IsAny<string>(), It.IsAny<PassUpdateReason>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldReturnFail_WhenAmountTooSmall()
    {
        // $5 con ratio 10 = 0 pts → falla
        var customer = NewCustomer();
        var card = NewCard(customer.Id);
        var setup = BuildHandler(card, customer);

        var result = await setup.Handler.Handle(
            new AddPointsCommand("KB-TEST001", 5m, "test"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("muy bajo", result.Error);
    }
}
