using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Notifications;
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
        Mock<ILoyaltyNotificationService> Notifications,
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

        var notifications = NotificationService(card, customer, now);
        var clock = Clock(now);
        var uow = NoOpUnitOfWork();

        var handler = new AddPointsHandler(
            cards.Object,
            customers.Object,
            transactions.Object,
            pointLots.Object,
            config.Object,
            LevelCalculator().Object,
            notifications.Object,
            TenantContext().Object,
            clock.Object,
            uow.Object,
            NullLogger<AddPointsHandler>.Instance);

        return new HandlerSetup(handler, cards, customers, transactions, notifications, uow);
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
    public async Task Handle_ShouldCreatePointsAddedNotification_WhenPointsAdded()
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

        var notifications = NotificationService(card, customer);
        var clock = Clock();
        var uow = NoOpUnitOfWork();

        var handler = new AddPointsHandler(
            cards.Object, customers.Object, transactions.Object, pointLots.Object, config.Object,
            LevelCalculator().Object, notifications.Object, TenantContext().Object, clock.Object, uow.Object,
            NullLogger<AddPointsHandler>.Instance);

        await handler.Handle(
            new AddPointsCommand("KB-TEST001", 200m, "test"),
            CancellationToken.None);

        notifications.Verify(s => s.CreateAsync(
            It.Is<CreateLoyaltyNotificationRequest>(request =>
                request.SerialNumber == "KB-TEST001" &&
                request.Type == NotificationType.PointsAdded &&
                request.Channels.Contains(NotificationChannel.AppleWallet) &&
                request.CorrelationId != null &&
                request.CorrelationId.StartsWith("points-added:", StringComparison.Ordinal) &&
                request.MetadataJson != null &&
                request.MetadataJson.Contains("\"pointsAdded\":20", StringComparison.Ordinal) &&
                request.MetadataJson.Contains("\"previousPoints\":0", StringComparison.Ordinal) &&
                request.MetadataJson.Contains("\"newTotal\":20", StringComparison.Ordinal) &&
                request.ProcessImmediately),
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
        setup.Notifications.Verify(s => s.CreateAsync(
            It.IsAny<CreateLoyaltyNotificationRequest>(), It.IsAny<CancellationToken>()),
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

    private static Mock<ILoyaltyNotificationService> NotificationService(
        LoyaltyCard? card,
        Customer? customer,
        DateTime? now = null)
    {
        var mock = new Mock<ILoyaltyNotificationService>();
        mock.Setup(s => s.CreateAsync(
                It.IsAny<CreateLoyaltyNotificationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateLoyaltyNotificationRequest request, CancellationToken _) =>
                new NotificationDto(
                    Guid.NewGuid(),
                    customer?.Id ?? Guid.NewGuid(),
                    card?.Id ?? Guid.NewGuid(),
                    customer?.FullName,
                    request.SerialNumber,
                    request.Type,
                    request.Title,
                    request.Message,
                    NotificationStatus.Delivered,
                    now ?? Now,
                    request.ScheduledAtUtc,
                    request.DisplayUntilUtc,
                    now ?? Now,
                    request.CorrelationId,
                    request.Source,
                    request.CustomNotificationCampaignId,
                    request.ShortMessage,
                    request.LongMessage,
                    null,
                    []));
        return mock;
    }
}
