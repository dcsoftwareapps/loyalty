using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Tools;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class WalletProductionUpdateDiagnosticsTests
{
    private static readonly Guid TenantId = Guid.Parse("b1000000-0000-0000-0000-000000000001");

    [Fact]
    [Trait("Category", "WalletProductionUpdate")]
    public async Task Wallet_diagnostics_reports_registration_without_exposing_tokens()
    {
        var tenantContext = new TestTenantContext();
        tenantContext.SetTenant(TenantId, "kbeauty");
        await using var db = CreateDbContext(tenantContext);
        await SeedAsync(db);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Wallet:UseRealApns"] = "true",
                ["Wallet:UseRealPassSigning"] = "true"
            })
            .Build();
        var options = Options.Create(new ApplePassOptions
        {
            PassTypeIdentifier = "pass.com.kbeautymx.loyalty",
            TeamIdentifier = "HS2XCFGQ75",
            WebServiceURL = "https://loyaltycloud-api-894839.azurewebsites.net",
            OrganizationName = "KBeauty MX",
            ApnHost = "https://api.push.apple.com"
        });
        var tool = new WalletDiagnosticsTool(db, options, configuration);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await tool.RunAsync("KB-LNB7ACG", output, error);
        var text = output.ToString();

        Assert.Equal(0, exitCode);
        Assert.Contains("TenantSlug: kbeauty", text);
        Assert.Contains("Device registrations count: 1", text);
        Assert.Contains("Current card points: 100", text);
        Assert.Contains("Configured WebServiceURL: https://loyaltycloud-api-894839.azurewebsites.net", text);
        Assert.Contains("Configured APNs host: https://api.push.apple.com", text);
        Assert.Contains("pushToken=0123...CDEF", text);
        Assert.DoesNotContain("0123456789ABCDEF", text);
        Assert.DoesNotContain("secret-auth-token", text);
    }

    private static AppDbContext CreateDbContext(ITenantContext tenantContext)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"wallet-diagnostics-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options, new NoopPublisher(), tenantContext);
    }

    private static async Task SeedAsync(AppDbContext db)
    {
        var now = DateTime.UtcNow;
        var customer = new Customer(
            Guid.Parse("b1000000-0000-0000-0000-000000000101"),
            TenantId,
            "Daniel Chavez",
            "daniel@example.local",
            new DateTime(1990, 1, 1),
            now,
            "6461234567");
        var card = new LoyaltyCard(
            Guid.Parse("b1000000-0000-0000-0000-000000000201"),
            TenantId,
            customer.Id,
            "KB-LNB7ACG",
            now);
        card.EarnPoints(
            100,
            LoyaltyCloud.Domain.Enums.TransactionType.Purchase,
            LoyaltyCloud.Domain.ValueObjects.ProgramConfigSnapshot.FromEntries([]),
            new FixedClock(now));

        db.Tenants.Add(new Tenant(TenantId, "kbeauty", "KBeauty", "America/Tijuana", now));
        db.Customers.Add(customer);
        db.LoyaltyCards.Add(card);
        db.DeviceRegistrations.Add(new DeviceRegistration(
            Guid.Parse("b1000000-0000-0000-0000-000000000301"),
            TenantId,
            "device-library-abcdef",
            "pass.com.kbeautymx.loyalty",
            "KB-LNB7ACG",
            "0123456789ABCDEF",
            now));
        await db.SaveChangesAsync();
    }

    private sealed class TestTenantContext : IMutableTenantContext
    {
        public Guid? TenantId { get; private set; }
        public string? TenantSlug { get; private set; }
        public bool HasTenant => TenantId.HasValue && !string.IsNullOrWhiteSpace(TenantSlug);

        public void SetTenant(Guid tenantId, string tenantSlug)
        {
            TenantId = tenantId;
            TenantSlug = tenantSlug;
        }

        public void Clear()
        {
            TenantId = null;
            TenantSlug = null;
        }
    }

    private sealed class NoopPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }

    private sealed class FixedClock : LoyaltyCloud.Common.Services.IDateTimeProvider
    {
        public FixedClock(DateTime utcNow) => UtcNow = utcNow;
        public DateTime UtcNow { get; }
        public DateTime Today => UtcNow.Date;
    }
}
