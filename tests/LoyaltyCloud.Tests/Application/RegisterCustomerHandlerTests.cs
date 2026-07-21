using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Customers.Commands.RegisterCustomer;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using static LoyaltyCloud.Tests.Application.HandlerTestHelpers;

namespace LoyaltyCloud.Tests.Application;

public class RegisterCustomerHandlerTests
{
    private static RegisterCustomerHandler BuildHandler(
        Mock<ICustomerRepository> customers,
        Mock<ILoyaltyCardRepository> cards,
        Mock<IPointTransactionRepository> transactions,
        Mock<IUnitOfWork>? uow = null,
        Mock<IPassGeneratorService>? passes = null,
        Mock<IStorageService>? storage = null,
        Mock<IPointLotRepository>? pointLots = null)
    {
        var config = ConfigRepoWithDefaults();
        transactions.Setup(r => r.GetEligibleLevelPointsAsync(
                It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var passesMock = passes ?? new Mock<IPassGeneratorService>();
        passesMock.Setup(p => p.GeneratePassAsync(It.IsAny<LoyaltyCard>(), It.IsAny<Customer>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new byte[] { 1, 2, 3 });

        var storageMock = storage ?? new Mock<IStorageService>();
        storageMock.Setup(s => s.UploadPassAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync("https://blob.test/pass.pkpass");

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(c => c.UserName).Returns("admin");

        return new RegisterCustomerHandler(
            customers.Object,
            cards.Object,
            transactions.Object,
            (pointLots ?? new Mock<IPointLotRepository>()).Object,
            config.Object,
            passesMock.Object,
            storageMock.Object,
            LevelCalculator().Object,
            TenantContext().Object,
            Clock().Object,
            currentUser.Object,
            (uow ?? NoOpUnitOfWork()).Object,
            NullLogger<RegisterCustomerHandler>.Instance);
    }

    // =========================================================================

    [Fact]
    public async Task Handle_ShouldCreateCustomerAndCard_WhenEmailIsUnique()
    {
        var customers = new Mock<ICustomerRepository>();
        customers.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(false);

        var cards = new Mock<ILoyaltyCardRepository>();
        var transactions = new Mock<IPointTransactionRepository>();
        var uow = NoOpUnitOfWork();

        var handler = BuildHandler(customers, cards, transactions, uow);

        var cmd = new RegisterCustomerCommand(
            FullName: "Ana López",
            Email: "ana@test.com",
            DateOfBirth: new DateTime(1990, 1, 1),
            Phone: "555-1234");

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value.SerialNumber);
        Assert.StartsWith("KB-", result.Value.SerialNumber);

        customers.Verify(r => r.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()), Times.Once);
        cards.Verify(r => r.AddAsync(It.IsAny<LoyaltyCard>(), It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnFail_WhenEmailAlreadyExists()
    {
        var customers = new Mock<ICustomerRepository>();
        customers.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);

        var cards = new Mock<ILoyaltyCardRepository>();
        var transactions = new Mock<IPointTransactionRepository>();
        var uow = NoOpUnitOfWork();
        var handler = BuildHandler(customers, cards, transactions, uow);

        var result = await handler.Handle(
            new RegisterCustomerCommand("Ana", "duplicate@test.com", new DateTime(1990, 1, 1)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Ya existe", result.Error);
        customers.Verify(r => r.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()), Times.Never);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldAddWelcomeBonus_OnRegistration()
    {
        var customers = new Mock<ICustomerRepository>();
        customers.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(false);

        var cards = new Mock<ILoyaltyCardRepository>();
        LoyaltyCard? addedCard = null;
        cards.Setup(r => r.AddAsync(It.IsAny<LoyaltyCard>(), It.IsAny<CancellationToken>()))
             .Callback<LoyaltyCard, CancellationToken>((c, _) => addedCard = c);

        var transactions = new Mock<IPointTransactionRepository>();
        var handler = BuildHandler(customers, cards, transactions);

        var result = await handler.Handle(
            new RegisterCustomerCommand("Ana", "ana@test.com", new DateTime(1990, 1, 1)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(addedCard);
        Assert.Equal(50, addedCard!.CurrentPoints); // welcome bonus default = 50
        Assert.Equal(50, result.Value.CurrentPoints);

        transactions.Verify(t => t.AddAsync(It.IsAny<PointTransaction>(), It.IsAny<CancellationToken>()),
            Times.Once); // un PointTransaction de bienvenida
    }

    [Fact]
    public async Task Handle_ShouldAddReferralPoints_WhenReferrerExists()
    {
        var customers = new Mock<ICustomerRepository>();
        customers.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(false);

        var referrerCard = NewCard(serial: "KB-REFER001");
        var cards = new Mock<ILoyaltyCardRepository>();
        cards.Setup(r => r.GetBySerialNumberAsync("KB-REFER001", It.IsAny<CancellationToken>()))
             .ReturnsAsync(referrerCard);

        var transactions = new Mock<IPointTransactionRepository>();
        var addedTransactions = new List<PointTransaction>();
        transactions.Setup(t => t.AddAsync(It.IsAny<PointTransaction>(), It.IsAny<CancellationToken>()))
                    .Callback<PointTransaction, CancellationToken>((tx, _) => addedTransactions.Add(tx));

        var handler = BuildHandler(customers, cards, transactions);

        var result = await handler.Handle(
            new RegisterCustomerCommand(
                "Beatriz",
                "bea@test.com",
                new DateTime(1992, 5, 1),
                Phone: null,
                ReferredBySerialNumber: "KB-REFER001"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        // Dos transacciones: welcome para la nueva clienta + referral para la referidora
        Assert.Equal(2, addedTransactions.Count);
        Assert.Contains(addedTransactions, t => t.LoyaltyCardId == referrerCard.Id && t.Points == 150);

        // La referidora recibió 150 pts.
        Assert.Equal(150, referrerCard.CurrentPoints);
    }

    [Fact]
    public async Task Handle_ShouldReturnFail_WhenReferrerSerialDoesNotExist()
    {
        var customers = new Mock<ICustomerRepository>();
        customers.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(false);

        var cards = new Mock<ILoyaltyCardRepository>();
        cards.Setup(r => r.GetBySerialNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((LoyaltyCard?)null);

        var transactions = new Mock<IPointTransactionRepository>();
        var handler = BuildHandler(customers, cards, transactions);

        var result = await handler.Handle(
            new RegisterCustomerCommand(
                "Beatriz", "bea@test.com", new DateTime(1992, 5, 1),
                ReferredBySerialNumber: "KB-NOEXIST"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("referido", result.Error);
    }
}
