using LoyaltyCloud.Application.Admin.Queries.GetReportsSummary;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class ReportsSummaryTests
{
    private static readonly Guid TenantA = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("b1000000-0000-0000-0000-000000000001");
    private static readonly DateTime Now = new(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Category", "Reports")]
    public async Task Summary_counts_period_metrics_without_double_counting_active_customers()
    {
        var tenantContext = CreateTenantContext(TenantA, "tenant-a");
        await using var db = CreateDb(tenantContext);
        SeedTenant(db, TenantA, "tenant-a");

        var customer = AddCustomerWithCard(db, TenantA, "Ana Lopez", "KB-REPORT1", createdDaysAgo: 3, currentPoints: 250);
        db.PointTransactions.Add(new PointTransaction(
            Guid.NewGuid(),
            TenantA,
            customer.CardId,
            points: 100,
            TransactionType.Purchase,
            "Compra",
            Now.AddDays(-2),
            purchaseAmount: 500m));
        db.PointTransactions.Add(new PointTransaction(
            Guid.NewGuid(),
            TenantA,
            customer.CardId,
            points: 25,
            TransactionType.BonusWelcome,
            "Bienvenida",
            Now.AddDays(-2)));
        db.PointTransactions.Add(new PointTransaction(
            Guid.NewGuid(),
            TenantA,
            customer.CardId,
            points: 50,
            TransactionType.RedemptionReversal,
            "Reversa",
            Now.AddDays(-2)));
        db.PointTransactions.Add(new PointTransaction(
            Guid.NewGuid(),
            TenantA,
            customer.CardId,
            points: -15,
            TransactionType.Expired,
            "Expiracion",
            Now.AddDays(-1)));
        db.Redemptions.Add(new Redemption(
            Guid.NewGuid(),
            TenantA,
            customer.CardId,
            pointsSpent: 40,
            monetaryAmount: 40m,
            monetaryCurrency: "MXN",
            pointsPerPesoUnit: 1m,
            redeemedAtUtc: Now.AddDays(-1)));

        await db.SaveChangesAsync();

        var summary = await ReadForTenant(db, tenantContext);

        Assert.Equal(1, summary.PeriodMetrics.NewCustomers);
        Assert.Equal(1, summary.PeriodMetrics.ActiveCustomers);
        Assert.Equal(1, summary.PeriodMetrics.RegisteredPurchases);
        Assert.Equal(500m, summary.PeriodMetrics.RegisteredPurchaseAmount);
        Assert.Equal(125, summary.PeriodMetrics.PointsIssued);
        Assert.Equal(40, summary.PeriodMetrics.PointsRedeemed);
        Assert.Equal(15, summary.PeriodMetrics.PointsExpired);
        Assert.Equal(1, summary.PeriodMetrics.Redemptions);
        Assert.Equal(1, summary.CurrentProgram.TotalCustomers);
        Assert.Equal(250, summary.CurrentProgram.CurrentPointBalance);
    }

    [Fact]
    [Trait("Category", "Reports")]
    public async Task Inactive_customers_use_created_at_for_never_active_customers_and_exclude_new_customers()
    {
        var tenantContext = CreateTenantContext(TenantA, "tenant-a");
        await using var db = CreateDb(tenantContext);
        SeedTenant(db, TenantA, "tenant-a");

        AddCustomerWithCard(db, TenantA, "Cliente Viejo", "KB-OLD", createdDaysAgo: 120, currentPoints: 10);
        AddCustomerWithCard(db, TenantA, "Cliente Nuevo", "KB-NEW", createdDaysAgo: 10, currentPoints: 0);

        await db.SaveChangesAsync();

        var summary = await ReadForTenant(db, tenantContext, inactiveDays: 90);

        var inactive = Assert.Single(summary.InactiveCustomers.Items);
        Assert.Equal("Cliente Viejo", inactive.CustomerName);
        Assert.Equal(120, inactive.DaysWithoutActivity);
        Assert.Equal(1, summary.InactiveCustomers.Total);
    }

    [Fact]
    [Trait("Category", "Reports")]
    public async Task Inactive_customers_report_uses_own_threshold()
    {
        var tenantContext = CreateTenantContext(TenantA, "tenant-a");
        await using var db = CreateDb(tenantContext);
        SeedTenant(db, TenantA, "tenant-a");

        AddCustomerWithCard(db, TenantA, "Cliente Sesenta", "KB-60", createdDaysAgo: 70, currentPoints: 10);
        AddCustomerWithCard(db, TenantA, "Cliente Ciento veinte", "KB-120", createdDaysAgo: 120, currentPoints: 10);

        await db.SaveChangesAsync();

        var service = new ReportsReadService(db, tenantContext, new FixedClock(Now));
        var report = await service.GetInactiveCustomersAsync(new GetInactiveCustomersReportQuery(90));

        var inactive = Assert.Single(report.Items);
        Assert.Equal("Cliente Ciento veinte", inactive.CustomerName);
        Assert.Equal(90, report.ThresholdDays);
    }

    [Fact]
    [Trait("Category", "Reports")]
    public async Task Inactive_customers_treat_redemption_as_activity()
    {
        var tenantContext = CreateTenantContext(TenantA, "tenant-a");
        await using var db = CreateDb(tenantContext);
        SeedTenant(db, TenantA, "tenant-a");

        var customer = AddCustomerWithCard(db, TenantA, "Cliente Canje", "KB-RED", createdDaysAgo: 140, currentPoints: 50);
        db.Redemptions.Add(new Redemption(
            Guid.NewGuid(),
            TenantA,
            customer.CardId,
            pointsSpent: 20,
            monetaryAmount: 20m,
            monetaryCurrency: "MXN",
            pointsPerPesoUnit: 1m,
            redeemedAtUtc: Now.AddDays(-5)));

        await db.SaveChangesAsync();

        var summary = await ReadForTenant(db, tenantContext, inactiveDays: 90);

        Assert.Empty(summary.InactiveCustomers.Items);
    }

    [Fact]
    [Trait("Category", "Reports")]
    public async Task Top_rewards_counts_catalog_redemptions_and_ignores_monetary_discounts()
    {
        var tenantContext = CreateTenantContext(TenantA, "tenant-a");
        await using var db = CreateDb(tenantContext);
        SeedTenant(db, TenantA, "tenant-a");

        var customer = AddCustomerWithCard(db, TenantA, "Cliente Premio", "KB-PRIZE", createdDaysAgo: 20, currentPoints: 300);
        var reward = new RewardCatalogItem(
            Guid.NewGuid(),
            TenantA,
            "Facial",
            "Facial de regalo",
            pointsCost: 100,
            minLevel: string.Empty);
        db.RewardCatalogItems.Add(reward);
        db.Redemptions.Add(new Redemption(Guid.NewGuid(), TenantA, customer.CardId, reward.Id, 100, Now.AddDays(-2)));
        db.Redemptions.Add(new Redemption(Guid.NewGuid(), TenantA, customer.CardId, reward.Id, 100, Now.AddDays(-1)));
        db.Redemptions.Add(new Redemption(
            Guid.NewGuid(),
            TenantA,
            customer.CardId,
            pointsSpent: 50,
            monetaryAmount: 50m,
            monetaryCurrency: "MXN",
            pointsPerPesoUnit: 1m,
            redeemedAtUtc: Now.AddDays(-1)));

        await db.SaveChangesAsync();

        var summary = await ReadForTenant(db, tenantContext);

        var topReward = Assert.Single(summary.TopRewards);
        Assert.Equal("Facial", topReward.RewardName);
        Assert.Equal(2, topReward.Redemptions);
        Assert.Equal(3, summary.PeriodMetrics.Redemptions);
    }

    [Fact]
    [Trait("Category", "Reports")]
    public async Task Top_rewards_report_uses_own_period()
    {
        var tenantContext = CreateTenantContext(TenantA, "tenant-a");
        await using var db = CreateDb(tenantContext);
        SeedTenant(db, TenantA, "tenant-a");

        var customer = AddCustomerWithCard(db, TenantA, "Cliente Premio", "KB-TOP", createdDaysAgo: 20, currentPoints: 300);
        var reward = new RewardCatalogItem(
            Guid.NewGuid(),
            TenantA,
            "Spa",
            "Spa de regalo",
            pointsCost: 100,
            minLevel: string.Empty);
        db.RewardCatalogItems.Add(reward);
        db.Redemptions.Add(new Redemption(Guid.NewGuid(), TenantA, customer.CardId, reward.Id, 100, Now.AddDays(-2)));
        db.Redemptions.Add(new Redemption(Guid.NewGuid(), TenantA, customer.CardId, reward.Id, 100, Now.AddDays(-40)));

        await db.SaveChangesAsync();

        var service = new ReportsReadService(db, tenantContext, new FixedClock(Now));
        var rewards = await service.GetTopRewardsAsync(new GetTopRewardsReportQuery(Now.AddDays(-7), Now.AddDays(1)));

        var topReward = Assert.Single(rewards);
        Assert.Equal("Spa", topReward.RewardName);
        Assert.Equal(1, topReward.Redemptions);
    }

    [Fact]
    [Trait("Category", "Reports")]
    public async Task Summary_is_tenant_isolated()
    {
        var tenantContext = CreateTenantContext(TenantA, "tenant-a");
        var dbName = "Reports-" + Guid.NewGuid().ToString("N");
        await using var db = CreateDb(tenantContext, dbName);
        SeedTenant(db, TenantA, "tenant-a");
        var tenantACustomer = AddCustomerWithCard(db, TenantA, "Tenant A", "A-001", createdDaysAgo: 2, currentPoints: 100);
        db.PointTransactions.Add(new PointTransaction(Guid.NewGuid(), TenantA, tenantACustomer.CardId, 10, TransactionType.Purchase, "A", Now.AddDays(-1), purchaseAmount: 100m));
        await db.SaveChangesAsync();

        var tenantBContext = CreateTenantContext(TenantB, "tenant-b");
        await using (var tenantBDb = CreateDb(tenantBContext, dbName))
        {
            SeedTenant(tenantBDb, TenantB, "tenant-b");
            var tenantBCustomer = AddCustomerWithCard(tenantBDb, TenantB, "Tenant B", "B-001", createdDaysAgo: 2, currentPoints: 900);
            tenantBDb.PointTransactions.Add(new PointTransaction(Guid.NewGuid(), TenantB, tenantBCustomer.CardId, 90, TransactionType.Purchase, "B", Now.AddDays(-1), purchaseAmount: 900m));
            await tenantBDb.SaveChangesAsync();
        }

        var summary = await ReadForTenant(db, tenantContext);

        Assert.Equal(1, summary.CurrentProgram.TotalCustomers);
        Assert.Equal(100, summary.CurrentProgram.CurrentPointBalance);
        Assert.Equal(10, summary.PeriodMetrics.PointsIssued);
        Assert.Equal(100m, summary.PeriodMetrics.RegisteredPurchaseAmount);
    }

    private static TenantContext CreateTenantContext(Guid tenantId, string tenantSlug)
    {
        var context = new TenantContext();
        context.SetTenant(tenantId, tenantSlug);
        return context;
    }

    private static AppDbContext CreateDb(TenantContext tenantContext, string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? "Reports-" + Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options, Mock.Of<IPublisher>(), tenantContext);
    }

    private static async Task<ReportsSummaryDto> ReadForTenant(
        AppDbContext db,
        TenantContext tenantContext,
        int inactiveDays = 90)
    {
        var service = new ReportsReadService(db, tenantContext, new FixedClock(Now));
        return await service.GetReportsSummaryAsync(new GetReportsSummaryQuery(
            PeriodStartUtc: Now.AddDays(-30),
            PeriodEndUtc: Now.AddDays(1),
            InactiveDaysThreshold: inactiveDays));
    }

    private static void SeedTenant(AppDbContext db, Guid tenantId, string slug)
    {
        db.Tenants.Add(new Tenant(tenantId, slug, slug, "America/Tijuana", Now.AddDays(-200)));
    }

    private static (Guid CustomerId, Guid CardId) AddCustomerWithCard(
        AppDbContext db,
        Guid tenantId,
        string name,
        string serial,
        int createdDaysAgo,
        int currentPoints)
    {
        var customerId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var customer = new Customer(
            customerId,
            tenantId,
            name,
            $"{serial.ToLowerInvariant()}@example.test",
            Customer.BirthdayNotCaptured,
            Now.AddDays(-createdDaysAgo),
            "6460000000");
        var card = new LoyaltyCard(cardId, tenantId, customerId, serial, Now.AddDays(-createdDaysAgo));

        if (currentPoints > 0)
        {
            card.EarnPoints(
                currentPoints,
                TransactionType.Purchase,
                LoyaltyCloud.Domain.ValueObjects.ProgramConfigSnapshot.FromEntries([]),
                new FixedClock(Now.AddDays(-createdDaysAgo)));
            card.ClearDomainEvents();
        }

        db.Customers.Add(customer);
        db.LoyaltyCards.Add(card);
        return (customerId, cardId);
    }

    private sealed class FixedClock(DateTime now) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = now;
        public DateTime Today => UtcNow.Date;
    }
}
