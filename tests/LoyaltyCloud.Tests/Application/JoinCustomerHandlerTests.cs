using LoyaltyCloud.Application.Customers.Commands.JoinCustomer;
using LoyaltyCloud.Application.Customers.Commands.RegisterCustomer;
using LoyaltyCloud.Common.Results;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Repositories;
using MediatR;
using Moq;
using Xunit;
using static LoyaltyCloud.Tests.Application.HandlerTestHelpers;

namespace LoyaltyCloud.Tests.Application;

public sealed class JoinCustomerHandlerTests
{
    [Fact]
    public async Task Handle_ShouldRecoverExistingCustomer_WhenRegisterHitsConcurrentPhoneDuplicateAndNameMatches()
    {
        var customer = new Customer(
            Guid.NewGuid(),
            KBeautyTenantId,
            "José García",
            "phone-6461234567@loyaltycloud.local",
            Customer.BirthdayNotCaptured,
            Now,
            "6461234567");
        var card = NewCard(customer.Id, "KB-EXISTING1");

        var customers = new Mock<ICustomerRepository>();
        customers.SetupSequence(r => r.GetByNormalizedPhoneAsync("6461234567", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null)
            .ReturnsAsync(customer);

        var cards = new Mock<ILoyaltyCardRepository>();
        cards.Setup(r => r.GetByCustomerIdAsync(customer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(card);

        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.IsAny<RegisterCustomerCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<RegisterCustomerResponse>("Ya existe un cliente con ese telefono en el tenant actual."));

        var handler = new JoinCustomerHandler(customers.Object, cards.Object, sender.Object);

        var result = await handler.Handle(new JoinCustomerCommand("Jose", "Garcia", "(646) 123-4567"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.AlreadyExists);
        Assert.Equal(customer.Id, result.Value.CustomerId);
        Assert.Equal(card.SerialNumber, result.Value.SerialNumber);
        Assert.Contains("Ya tienes una tarjeta", result.Value.Message);
    }

    [Fact]
    public async Task Handle_ShouldRejectExistingPhone_WhenNameDoesNotMatch()
    {
        var customer = new Customer(
            Guid.NewGuid(),
            KBeautyTenantId,
            "Daniel Chavez",
            "phone-6461234567@loyaltycloud.local",
            Customer.BirthdayNotCaptured,
            Now,
            "6461234567");

        var customers = new Mock<ICustomerRepository>();
        customers.Setup(r => r.GetByNormalizedPhoneAsync("6461234567", It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        var cards = new Mock<ILoyaltyCardRepository>();
        var sender = new Mock<ISender>();
        var handler = new JoinCustomerHandler(customers.Object, cards.Object, sender.Object);

        var result = await handler.Handle(new JoinCustomerCommand("Danny", "Chavez", "6461234567"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Este número de teléfono ya está registrado.", result.Error);
        cards.Verify(r => r.GetByCustomerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        sender.Verify(s => s.Send(It.IsAny<RegisterCustomerCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
