using LoyaltyCloud.Application.Customers;
using LoyaltyCloud.Application.Customers.Commands.JoinCustomer;
using LoyaltyCloud.Application.Customers.Commands.RegisterCustomer;
using LoyaltyCloud.Domain.Entities;
using Xunit;

namespace LoyaltyCloud.Tests.Application;

public sealed class OptionalCustomerPhoneValidationTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("(646) 123-4567", true)]
    [InlineData("abc", false)]
    [InlineData("123", false)]
    [InlineData("1234567890123456789012345678901", false)]
    public void Public_join_validates_only_provided_phone(string? phone, bool valid)
    {
        Assert.Equal(valid, new JoinCustomerValidator().Validate(new JoinCustomerCommand("Ana", "Lopez", phone)).IsValid);
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("   ")]
    public void Domain_and_direct_registration_accept_absence_as_null(string? phone)
    {
        var customer = new Customer(Guid.NewGuid(), Guid.NewGuid(), "Ana Lopez", "ana@example.test",
            Customer.BirthdayNotCaptured, DateTime.UtcNow, phone);
        Assert.Null(customer.Phone);
        Assert.Null(customer.NormalizedPhone);
        Assert.Equal(string.Empty, CustomerPhoneNormalizer.Normalize(phone));
        Assert.True(new RegisterCustomerValidator().Validate(new RegisterCustomerCommand(
            "Ana Lopez", "ana@example.test", Customer.BirthdayNotCaptured, phone)).IsValid);
    }
}
