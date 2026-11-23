using LoyaltyCloud.Application.Customers.Commands.JoinCustomer;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Customers.Queries.GetCustomers;
using LoyaltyCloud.Application.Points.Commands.AddPoints;
using LoyaltyCloud.Application.Redemptions.Commands.RedeemReward;
using LoyaltyCloud.Application.Rewards.Commands.CreateReward;
using LoyaltyCloud.Application.Stamps;
using LoyaltyCloud.Common.Pagination;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Env = LoyaltyCloud.Tests.Integration.TenantProvisioningTests.ProvisioningTestEnvironment;

namespace LoyaltyCloud.Tests.Integration;

public sealed class OptionalCustomerPhoneTests
{
    [Theory]
    [InlineData(LoyaltyProgramType.Points, null)]
    [InlineData(LoyaltyProgramType.Points, "")]
    [InlineData(LoyaltyProgramType.Points, "   ")]
    [InlineData(LoyaltyProgramType.Stamps, null)]
    [InlineData(LoyaltyProgramType.Stamps, "")]
    [InlineData(LoyaltyProgramType.Stamps, "   ")]
    public async Task Missing_phone_creates_independent_cards_and_preserves_existing_contact(LoyaltyProgramType program, string? phone)
    {
        await using var env = await Env.CreateAsync();
        const string slug = "optional-phone";
        var tenant = await env.ProvisionAsync(slug, "Optional Phone", "owner", "Owner123!", programType: program);
        Assert.True(tenant.IsSuccess, tenant.Error);
        var id = tenant.Value.TenantId;
        if (program == LoyaltyProgramType.Stamps)
            Assert.True((await env.ConfigureStampsAsync(id, slug, welcome: 0)).IsSuccess);

        var existing = await env.SendAsync(id, slug, new JoinCustomerCommand("Ana", "Lopez", "6461234567"));
        var first = await env.SendAsync(id, slug, new JoinCustomerCommand("Ana", "Lopez", phone));
        var second = await env.SendAsync(id, slug, new JoinCustomerCommand("Ana", "Lopez", phone));
        Assert.True(existing.IsSuccess, existing.Error);
        Assert.True(first.IsSuccess, first.Error);
        Assert.True(second.IsSuccess, second.Error);
        Assert.False(first.Value.AlreadyExists);
        Assert.False(second.Value.AlreadyExists);
        Assert.Null(first.Value.Phone);
        Assert.NotEqual(first.Value.CustomerId, second.Value.CustomerId);
        Assert.NotEqual(first.Value.SerialNumber, second.Value.SerialNumber);
        Assert.NotEqual(existing.Value.CustomerId, first.Value.CustomerId);

        await env.TenantReadAsync(id, slug, async db =>
        {
            var customers = await db.Customers.ToListAsync();
            Assert.Equal(3, customers.Count);
            Assert.All(customers.Where(c => c.Id != existing.Value.CustomerId), c =>
            {
                Assert.Null(c.Phone);
                Assert.Null(c.NormalizedPhone);
            });
            Assert.Equal("6461234567", customers.Single(c => c.Id == existing.Value.CustomerId).Phone);
            return true;
        });
        var recovered = await env.SendAsync(id, slug, new JoinCustomerCommand("Ana", "Lopez", "(646) 123-4567"));
        Assert.True(recovered.IsSuccess, recovered.Error);
        Assert.True(recovered.Value.AlreadyExists);
        Assert.Equal(existing.Value.CustomerId, recovered.Value.CustomerId);

        var list = await env.SendAsync(id, slug, new GetCustomersQuery("Ana", null, new PaginationParams()));
        Assert.True(list.IsSuccess, list.Error);
        Assert.Equal(3, list.Value.Items.Count);
        await env.InTenantScopeAsync(id, slug, async services =>
        {
            var detail = await services.GetRequiredService<ICustomerDetailReadService>()
                .GetByCustomerIdAsync(first.Value.CustomerId);
            Assert.NotNull(detail);
            Assert.Null(detail.Summary.Phone);
            var customerRepository = services.GetRequiredService<LoyaltyCloud.Domain.Repositories.ICustomerRepository>();
            Assert.Null(await customerRepository.GetByNormalizedPhoneAsync(""));
            Assert.Null(await customerRepository.GetByNormalizedPhoneAsync("   "));
            return true;
        });

        if (program == LoyaltyProgramType.Stamps)
        {
            StampOperationResult? last = null;
            for (var i = 0; i < 10; i++)
            {
                var earn = await env.SendAsOperatorAsync(id, slug, new EarnStampCommand(first.Value.SerialNumber, $"earn-{i}"));
                Assert.True(earn.IsSuccess, earn.Error);
                last = earn.Value;
            }
            var reward = Assert.Single(last!.UnlockedRewards);
            var redeemed = await env.SendAsOperatorAsync(id, slug,
                new RedeemStampRewardCommand(first.Value.SerialNumber, reward.Id, "redeem-no-phone"));
            Assert.True(redeemed.IsSuccess, redeemed.Error);
            Assert.Equal(0, redeemed.Value.State.CurrentStamps);
        }
        else
        {
            var earned = await env.SendAsync(id, slug, new AddPointsCommand(first.Value.SerialNumber, 100, "owner"));
            Assert.True(earned.IsSuccess, earned.Error);
            var reward = await env.SendAsync(id, slug, new CreateRewardCommand("Reward", "Test", 1, "Mist", false, null, null, true));
            Assert.True(reward.IsSuccess, reward.Error);
            var redeemed = await env.SendAsync(id, slug, new RedeemRewardCommand(first.Value.SerialNumber, reward.Value.Id, "owner"));
            Assert.True(redeemed.IsSuccess, redeemed.Error);
        }
    }

    [Fact]
    public async Task Optional_phone_registration_and_recovery_remain_tenant_isolated()
    {
        await using var env = await Env.CreateAsync();
        var a = await env.ProvisionAsync("phone-a", "A", "owner", "Owner123!");
        var b = await env.ProvisionAsync("phone-b", "B", "owner", "Owner123!");
        Assert.True(a.IsSuccess, a.Error);
        Assert.True(b.IsSuccess, b.Error);
        foreach (var phone in new string?[] { null, "6461234567" })
        {
            var first = await env.SendAsync(a.Value.TenantId, "phone-a", new JoinCustomerCommand("Ana", "Lopez", phone));
            var second = await env.SendAsync(b.Value.TenantId, "phone-b", new JoinCustomerCommand("Ana", "Lopez", phone));
            Assert.True(first.IsSuccess, first.Error);
            Assert.True(second.IsSuccess, second.Error);
            Assert.NotEqual(first.Value.CustomerId, second.Value.CustomerId);
            Assert.NotEqual(first.Value.SerialNumber, second.Value.SerialNumber);
            Assert.False(second.Value.AlreadyExists);
        }
    }
}
