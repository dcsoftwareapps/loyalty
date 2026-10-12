using System.Text.Json;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Stamps;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LoyaltyCloud.Tests.Infrastructure;

[Trait("Category", "StampContext")]
public sealed class StampPassJsonTests
{
    [Theory]
    [InlineData("Earn", false)] [InlineData("Earn", true)] [InlineData("Reversal", false)] [InlineData("Rollover", false)] [InlineData("Rollover", true)]
    public void Context_uses_one_stable_progress_field_and_valid_placeholder(string operation, bool unlocked)
    {
        var message = unlocked ? "🎁 ¡Desbloqueaste Café gratis! Ahora tienes %@."
            : operation == "Reversal" ? "Se canceló el último sello. Ahora tienes %@." : "🎉 ¡Conseguiste un sello más! Ahora tienes %@.";
        var context = new StampWalletEvent(Guid.NewGuid(), operation == "Rollover" ? "Earn" : operation, operation == "Rollover", unlocked ? "Café gratis" : "5 / 10", message, unlocked);
        var root = Build(context);
        var front = root.GetProperty("storeCard").GetProperty("auxiliaryFields");
        var field = Assert.Single(front.EnumerateArray());
        Assert.Equal("stamps", field.GetProperty("key").GetString()); Assert.Equal("5 / 10", field.GetProperty("value").GetString());
        Assert.Equal(message, field.GetProperty("changeMessage").GetString());
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(message, "%@").Cast<System.Text.RegularExpressions.Match>());
        Assert.Equal(front.GetRawText(), Build(context).GetProperty("storeCard").GetProperty("auxiliaryFields").GetRawText());
    }

    [Theory]
    [InlineData(true, true)] [InlineData(true, false)] [InlineData(false, true)]
    public void Manual_message_keeps_front_back_expiration_and_own_change_message(bool active, bool recent)
    {
        var now = DateTime.UtcNow;
        var message = new WalletCustomMessage(Guid.NewGuid(), "Aviso", "Mensaje corto", "Detalle largo", active ? now.AddDays(1) : now.AddDays(-1), "📣 %@");
        var root = Build(null, message, recent, now);
        var fields = root.GetProperty("storeCard").GetProperty("auxiliaryFields").EnumerateArray().ToArray();
        Assert.False(fields[0].TryGetProperty("changeMessage", out _));
        var custom = fields.Where(x => x.GetProperty("key").GetString() == "custom_message").ToArray();
        if (active)
        {
            var front = Assert.Single(custom); Assert.Equal("", front.GetProperty("label").GetString());
            Assert.Equal("Mensaje corto", front.GetProperty("value").GetString());
            Assert.Equal(recent, front.TryGetProperty("changeMessage", out var change));
            if (recent) Assert.Equal("📣 %@", change.GetString());
            var back = root.GetProperty("storeCard").GetProperty("backFields").EnumerateArray().Single(x => x.GetProperty("key").GetString() == "custom_message_detail");
            Assert.Equal("Aviso\n\nDetalle largo", back.GetProperty("value").GetString());
        }
        else { Assert.Empty(custom); Assert.DoesNotContain("custom_message_detail", root.GetRawText()); }
        Assert.DoesNotContain("NOVEDAD", root.GetRawText());
    }

    private static JsonElement Build(StampWalletEvent? stampEvent, WalletCustomMessage? message = null, bool recent = false, DateTime? date = null)
    {
        var now = date ?? DateTime.UtcNow; var id = Guid.NewGuid();
        var tenant = new TenantContext(); tenant.SetTenant(id, "test");
        var customer = new Customer(Guid.NewGuid(), id, "Ana", "test@test.local", Customer.BirthdayNotCaptured, now);
        var card = new LoyaltyCard(Guid.NewGuid(), id, customer.Id, "ST-JSON", now);
        var brand = new TenantWalletBrandingDto(id, "test", "Test", "Test", "Sellos", "rgb(255,255,255)", "rgb(0,0,0)", "rgb(0,0,0)", "#FFFFFF", null, null, 100, "CustomerName", null, "", "Cliente", true, false);
        var clock = new Mock<IDateTimeProvider>(); clock.SetupGet(x => x.UtcNow).Returns(now);
        var generator = new PassGeneratorService(null! /* JSON-only test: never signs a package */, new Mock<IWalletNotificationReadService>().Object,
            new Mock<ITenantWalletBrandingReadService>().Object, new Mock<ITenantWalletAssetProvider>().Object, new Mock<ITenantLoyaltyLevelReadService>().Object,
            tenant, new Mock<IPointTransactionRepository>().Object, new Mock<ILevelProgressService>().Object, clock.Object,
            Options.Create(new ApplePassOptions { PassTypeIdentifier = "pass.test", TeamIdentifier = "TEST", OrganizationName = "Test", WebServiceURL = "https://test.local" }), NullLogger<PassGeneratorService>.Instance);
        var projection = new StampWalletProjection(Guid.Empty, Guid.Empty, 5, 10, null, null, [], stampEvent);
        var notifications = new WalletNotificationContext(null, null, null, null, null, null, message, null,
            recent && message is not null ? new WalletRecentVisibleEvent(message.NotificationId, NotificationType.Custom, now, now, message.DisplayUntilUtc) : null);
        return JsonSerializer.SerializeToElement(generator.BuildStampsPassJson(card, customer, brand, projection, notifications));
    }
}
