using LoyaltyCloud.Application;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Constants;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using static LoyaltyCloud.Tests.Application.HandlerTestHelpers;

namespace LoyaltyCloud.Tests.Application;

public sealed class MemberWalletDataServiceTests
{
    [Fact]
    public async Task GetBySerialNumberAsync_ShouldProjectCustomerAndCardWithoutRecalculatingBusinessRules()
    {
        var customer = NewCustomer(fullName: "Ana Lopez");
        var card = NewCard(customer.Id);
        card.EarnPoints(
            125,
            LoyaltyCloud.Domain.Enums.TransactionType.Purchase,
            ProgramConfigSnapshot.FromEntries(Array.Empty<LoyaltyCloud.Domain.Entities.ProgramConfig>()),
            Clock().Object);

        var customers = new Mock<ICustomerRepository>();
        customers.Setup(r => r.GetByIdAsync(customer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        var cards = new Mock<ILoyaltyCardRepository>();
        cards.Setup(r => r.GetBySerialNumberAsync("KB-TEST001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(card);

        var transactions = new Mock<IPointTransactionRepository>();
        transactions.Setup(r => r.GetEligibleLevelPointsAsync(
                card.Id,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(125);

        var services = new ServiceCollection();
        services.AddApplication();
        services.AddSingleton(customers.Object);
        services.AddSingleton(cards.Object);
        services.AddSingleton(transactions.Object);
        services.AddSingleton(TenantLevels().Object);
        services.AddSingleton(TenantContext().Object);
        services.AddSingleton(Clock().Object);

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IMemberWalletDataService>();

        var result = await service.GetBySerialNumberAsync("KB-TEST001");

        Assert.True(result.IsSuccess);
        Assert.Equal(customer.Id, result.Value.CustomerId);
        Assert.Equal(card.Id, result.Value.LoyaltyCardId);
        Assert.Equal(card.SerialNumber, result.Value.SerialNumber);
        Assert.Equal("Ana Lopez", result.Value.FullName);
        Assert.Equal(125, result.Value.CurrentPoints);
        Assert.Equal(LoyaltyConstants.Levels.Mist, result.Value.Level);
        Assert.Equal(card.SerialNumber, result.Value.BarcodeValue);
        Assert.Equal("Ana", result.Value.DisplayName);
        Assert.Equal("125 pts", result.Value.PointsText);
        Assert.Equal("Mist \u2728", result.Value.LevelText);
        Assert.Equal(LoyaltyConstants.Levels.Glow, result.Value.NextLevelText);
        Assert.Equal("875 pts", result.Value.RemainingPointsText);
        Assert.Equal("Presenta este c\u00f3digo en caja", result.Value.BarcodeAlternateText);
    }
}
