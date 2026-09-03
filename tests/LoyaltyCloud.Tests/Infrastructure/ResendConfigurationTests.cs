using System.Text.Json;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LoyaltyCloud.Tests.Infrastructure;

public sealed class ResendConfigurationTests
{
    [Theory]
    [InlineData("https://admin.example.test", true)]
    [InlineData("https://localhost", false)]
    [InlineData("http://admin.example.test", false)]
    public async Task EffectiveProvider_UsesResendTransport_NotLegacyDatabaseLabel(string url, bool complete)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options, new Mock<IPublisher>().Object, new Mock<ITenantContext>().Object);
        var settings = new BillingSettings(Guid.NewGuid(), DateTime.UtcNow);
        settings.UpdateEmailNotifications(true, "Cloudflare", "noreply@example.test", "LoyaltyCloud", url, DateTime.UtcNow);
        db.BillingSettings.Add(settings);
        await db.SaveChangesAsync();
        const string fakeSecret = "test-only-not-a-real-key";
        var env = new Mock<IHostEnvironment>(); env.SetupGet(x => x.EnvironmentName).Returns(Environments.Production);
        var service = new BillingEmailConfigurationProvider(db,
            Options.Create(new EmailOptions { SmtpHost = "smtp.resend.com", Username = "resend", Password = fakeSecret }), env.Object);
        var result = await service.GetAsync();
        Assert.Equal("Resend", result.Provider);
        Assert.True(result.CredentialsConfigured);
        Assert.Equal(complete, result.IsComplete);
        Assert.DoesNotContain(fakeSecret, JsonSerializer.Serialize(result));
        Assert.Equal(url, result.ApplicationBaseUrl);
        Assert.Equal("Cloudflare", settings.EmailProvider);
    }
}
