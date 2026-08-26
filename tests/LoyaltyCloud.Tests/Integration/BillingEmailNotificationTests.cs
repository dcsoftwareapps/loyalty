using LoyaltyCloud.Application;
using LoyaltyCloud.Application.Billing;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class BillingEmailNotificationTests
{
    [Theory]
    [InlineData(BillingNotificationType.UpcomingCharge, "Tu suscripción se renovará próximamente")]
    [InlineData(BillingNotificationType.PaymentSucceeded, "Pago de suscripción confirmado")]
    [InlineData(BillingNotificationType.PaymentFailed, "No pudimos procesar tu renovación")]
    [InlineData(BillingNotificationType.AutoRenewDisabled, "Renovación automática desactivada")]
    [InlineData(BillingNotificationType.AutoRenewEnabled, "Renovación automática activada")]
    public async Task Notification_builds_professional_html_and_text(BillingNotificationType type, string subject)
    {
        var sender = new RecordingEmailSender();
        await using var provider = CreateProvider(true, sender);
        var service = provider.GetRequiredService<IBillingNotificationService>();

        await service.SendAsync(Notification(type));

        var email = Assert.Single(sender.Messages);
        Assert.Equal(subject, email.Subject);
        Assert.Contains("LoyaltyCloud", email.HtmlBody);
        Assert.Contains("Administrar suscripción", email.HtmlBody);
        Assert.Contains("LoyaltyCloud", email.TextBody);
        Assert.Contains("https://admin.test/spa/billing", email.TextBody);
        Assert.Contains("Spa Norte", email.HtmlBody);
    }

    [Fact]
    public async Task Missing_billing_contact_is_skipped()
    {
        var sender = new RecordingEmailSender();
        await using var provider = CreateProvider(true, sender);
        await provider.GetRequiredService<IBillingNotificationService>().SendAsync(Notification() with { Recipient = null });
        Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task Email_disabled_is_skipped()
    {
        var sender = new RecordingEmailSender();
        await using var provider = CreateProvider(false, sender);
        await provider.GetRequiredService<IBillingNotificationService>().SendAsync(Notification());
        Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task Provider_failure_is_controlled()
    {
        var sender = new RecordingEmailSender { Failure = new InvalidOperationException("provider unavailable") };
        await using var provider = CreateProvider(true, sender);
        var exception = await Record.ExceptionAsync(() => provider.GetRequiredService<IBillingNotificationService>().SendAsync(Notification()));
        Assert.Null(exception);
    }

    private static ServiceProvider CreateProvider(bool enabled, RecordingEmailSender sender)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\MSSQLLocalDB;Database=EmailTests;Trusted_Connection=True;",            ["Email:SmtpHost"] = "smtp.mx.cloudflare.net",
            ["Email:SmtpPort"] = "465",
            ["Email:Username"] = "api_token",
            ["Email:Password"] = "x",        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration, new DevelopmentEnvironment());
        services.RemoveAll<IBillingEmailConfigurationProvider>();
        services.AddSingleton<IBillingEmailConfigurationProvider>(new StaticEmailConfiguration(enabled));
        services.RemoveAll<ITransactionalEmailSender>();
        services.AddSingleton<ITransactionalEmailSender>(sender);
        return services.BuildServiceProvider();
    }

    private static BillingNotification Notification(BillingNotificationType type = BillingNotificationType.UpcomingCharge) =>
        new(Guid.NewGuid(), "billing@example.test", type, "evt_1", 290m, "MXN",
            new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc),
            "/spa/billing", "Spa Norte", 1,
            new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc), "Visa", "4242");

    private sealed class DevelopmentEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "LoyaltyCloud.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class StaticEmailConfiguration(bool enabled) : IBillingEmailConfigurationProvider
    {
        public Task<BillingEmailSettingsDto> GetAsync(CancellationToken ct = default) =>
            Task.FromResult(new BillingEmailSettingsDto(
                enabled, "Cloudflare", "notifications@example.test", "LoyaltyCloud",
                "https://admin.test", true, true));
    }
    private sealed class RecordingEmailSender : ITransactionalEmailSender
    {
        public List<TransactionalEmail> Messages { get; } = [];
        public Exception? Failure { get; init; }
        public Task SendAsync(TransactionalEmail email, CancellationToken ct = default)
        {
            if (Failure is not null) throw Failure;
            Messages.Add(email);
            return Task.CompletedTask;
        }
    }
}
