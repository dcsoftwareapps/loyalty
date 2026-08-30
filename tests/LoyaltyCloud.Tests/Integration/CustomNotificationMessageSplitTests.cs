using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Notifications.Custom.Commands.CreateCustomNotificationCampaign;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class CustomNotificationMessageSplitTests : IntegrationTestBase
{
    public CustomNotificationMessageSplitTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    [Trait("Category", "AdminMarketingNotifications")]
    public async Task Create_custom_message_persists_short_notification_and_long_detail()
    {
        var result = await WithTenantAsync(async sp =>
            await sp.GetRequiredService<ISender>().Send(new CreateCustomNotificationCampaignCommand(
                "Mensaje prueba split",
                "NOVEDAD",
                "Brillitos hoy",
                "Hoy tenemos brillitos de regalo al visitar la tienda.",
                CustomNotificationCampaign.AudienceAllWalletUsers,
                null,
                null,
                DateTime.UtcNow.AddHours(2),
                DateTime.UtcNow.AddDays(2),
                SendImmediately: false)));

        Assert.True(result.IsSuccess, result.Error);

        await WithTenantAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var campaign = await db.CustomNotificationCampaigns
                .SingleAsync(c => c.Id == result.Value.Id);

            Assert.Equal("Brillitos hoy", campaign.ShortMessage);
            Assert.Equal("Hoy tenemos brillitos de regalo al visitar la tienda.", campaign.LongMessage);
        });
    }

    [Theory]
    [InlineData("", "Detalle valido")]
    [InlineData("Notificacion valida", "")]
    [Trait("Category", "AdminMarketingNotifications")]
    public async Task Create_custom_message_requires_short_notification_and_long_detail(
        string shortMessage,
        string longMessage)
    {
        var result = await WithTenantAsync(async sp =>
            await sp.GetRequiredService<ISender>().Send(new CreateCustomNotificationCampaignCommand(
                "Mensaje invalido",
                "NOVEDAD",
                shortMessage,
                longMessage,
                CustomNotificationCampaign.AudienceAllWalletUsers,
                null,
                null,
                DateTime.UtcNow.AddHours(2),
                DateTime.UtcNow.AddDays(2),
                SendImmediately: false)));

        Assert.True(result.IsFailure);
    }

    private async Task<T> WithTenantAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider
            .GetRequiredService<IMutableTenantContext>()
            .SetTenant(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug);
        return await action(scope.ServiceProvider);
    }

    private async Task WithTenantAsync(Func<IServiceProvider, Task> action)
    {
        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider
            .GetRequiredService<IMutableTenantContext>()
            .SetTenant(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug);
        await action(scope.ServiceProvider);
    }
}
