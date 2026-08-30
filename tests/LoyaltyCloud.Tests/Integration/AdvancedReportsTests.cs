using LoyaltyCloud.Application.Admin.Queries.AdvancedReports;
using LoyaltyCloud.Application.Common.Interfaces;
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

public sealed class AdvancedReportsTests
{
    private static readonly Guid TenantA = Guid.Parse("a2000000-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("b2000000-0000-0000-0000-000000000001");
    private static readonly DateTime Now = new(2026, 8, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact, Trait("Category", "Reports")]
    public async Task Advanced_reports_are_tenant_isolated()
    {
        var database = "AdvancedReports-" + Guid.NewGuid().ToString("N");
        var contextA = TenantContextFor(TenantA, "tenant-a");
        await using var dbA = CreateDb(contextA, database);
        AddTenant(dbA, TenantA, "tenant-a");
        var cardA = AddCustomer(dbA, TenantA, "Ana", "A-1", 120);
        dbA.PointTransactions.Add(new PointTransaction(Guid.NewGuid(), TenantA, cardA, 100, TransactionType.Purchase, "Compra A", Now.AddDays(-2), purchaseAmount: 600m));
        await dbA.SaveChangesAsync();

        var contextB = TenantContextFor(TenantB, "tenant-b");
        await using (var dbB = CreateDb(contextB, database))
        {
            AddTenant(dbB, TenantB, "tenant-b");
            var cardB = AddCustomer(dbB, TenantB, "Bruno", "B-1", 900);
            dbB.PointTransactions.Add(new PointTransaction(Guid.NewGuid(), TenantB, cardB, 900, TransactionType.Purchase, "Compra B", Now.AddDays(-1), purchaseAmount: 9000m));
            await dbB.SaveChangesAsync();
        }

        var service = Service(dbA, contextA);
        var top = await service.GetTopCustomersAsync(new(Now.AddDays(-30), Now.AddDays(1), TopCustomerMetric.PointsEarned));
        var levels = await service.GetLevelDistributionAsync(new());

        Assert.Equal("Ana", Assert.Single(top.Customers).CustomerName);
        Assert.Equal(100, top.Customers[0].PointsEarned);
        Assert.Equal(1, levels.TotalCustomers);
    }

    [Fact, Trait("Category", "Reports")]
    public async Task Visit_frequency_counts_one_visit_per_customer_per_calendar_day()
    {
        var context = TenantContextFor(TenantA, "tenant-a");
        await using var db = CreateDb(context);
        AddTenant(db, TenantA, "tenant-a");
        var card = AddCustomer(db, TenantA, "Ana", "A-2", 50);
        db.PointTransactions.AddRange(
            new PointTransaction(Guid.NewGuid(), TenantA, card, 10, TransactionType.Purchase, "Mañana", Now.AddDays(-3).Date.AddHours(9)),
            new PointTransaction(Guid.NewGuid(), TenantA, card, 15, TransactionType.BonusWelcome, "Tarde", Now.AddDays(-3).Date.AddHours(16)),
            new PointTransaction(Guid.NewGuid(), TenantA, card, 20, TransactionType.Purchase, "Regreso", Now.AddDays(-1).Date.AddHours(10)));
        await db.SaveChangesAsync();

        var report = await Service(db, context).GetVisitFrequencyAsync(new(Now.AddDays(-30), Now.AddDays(1)));

        var customer = Assert.Single(report.Customers);
        Assert.Equal(2, customer.Visits);
        Assert.Equal(2m, customer.AverageDaysBetweenVisits);
        Assert.Equal(1, report.TwoToThreeVisits);
    }

    [Fact, Trait("Category", "Reports")]
    public async Task Returning_customers_separates_new_from_customers_who_return()
    {
        var context = TenantContextFor(TenantA, "tenant-a");
        await using var db = CreateDb(context);
        AddTenant(db, TenantA, "tenant-a");
        var returningCard = AddCustomer(db, TenantA, "Recurrente", "A-3", 80);
        var newCard = AddCustomer(db, TenantA, "Nueva", "A-4", 10);
        db.PointTransactions.AddRange(
            new PointTransaction(Guid.NewGuid(), TenantA, returningCard, 10, TransactionType.Purchase, "Antes", Now.AddDays(-50)),
            new PointTransaction(Guid.NewGuid(), TenantA, returningCard, 10, TransactionType.Purchase, "Regreso", Now.AddDays(-5)),
            new PointTransaction(Guid.NewGuid(), TenantA, newCard, 10, TransactionType.Purchase, "Primera", Now.AddDays(-4)));
        await db.SaveChangesAsync();

        var report = await Service(db, context).GetReturningCustomersAsync(new(Now.AddDays(-30), Now.AddDays(1)));

        Assert.Equal(2, report.ActiveCustomers);
        Assert.Equal(1, report.NewCustomers);
        Assert.Equal(1, report.ReturningCustomers);
        Assert.Equal(50m, report.ReturningPercentage);
    }

    [Fact, Trait("Category", "Reports")]
    public async Task Activity_and_level_reports_return_safe_empty_results()
    {
        var context = TenantContextFor(TenantA, "tenant-a");
        await using var db = CreateDb(context);
        AddTenant(db, TenantA, "tenant-a");
        await db.SaveChangesAsync();
        var service = Service(db, context);

        var activity = await service.GetActivityTrendsAsync(new(Now.AddMonths(-3), Now.AddDays(1)));
        var levels = await service.GetLevelDistributionAsync(new());

        Assert.NotEmpty(activity.Periods);
        Assert.All(activity.Periods, x => Assert.Equal(0, x.ActiveCustomers));
        Assert.Equal(0, levels.TotalCustomers);
        Assert.Empty(levels.Levels);
    }

    [Fact, Trait("Category", "Reports")]
    public async Task Top_customers_honors_period_metric_and_limit_filters()
    {
        var context = TenantContextFor(TenantA, "tenant-a");
        await using var db = CreateDb(context);
        AddTenant(db, TenantA, "tenant-a");
        var first = AddCustomer(db, TenantA, "Primera", "A-5", 10);
        var second = AddCustomer(db, TenantA, "Segunda", "A-6", 10);
        db.PointTransactions.AddRange(
            new PointTransaction(Guid.NewGuid(), TenantA, first, 10, TransactionType.Purchase, "Reciente", Now.AddDays(-2), purchaseAmount: 100m),
            new PointTransaction(Guid.NewGuid(), TenantA, second, 20, TransactionType.Purchase, "Reciente", Now.AddDays(-1), purchaseAmount: 900m),
            new PointTransaction(Guid.NewGuid(), TenantA, first, 500, TransactionType.Purchase, "Fuera", Now.AddDays(-90), purchaseAmount: 5000m));
        await db.SaveChangesAsync();

        var report = await Service(db, context).GetTopCustomersAsync(new(Now.AddDays(-30), Now.AddDays(1), TopCustomerMetric.PurchaseAmount, Limit: 1));

        var leader = Assert.Single(report.Customers);
        Assert.Equal("Segunda", leader.CustomerName);
        Assert.Equal(900m, leader.RankingValue);
    }

    [Fact, Trait("Category", "Reports")]
    public async Task Activity_trends_filters_dates_and_keeps_zero_value_months()
    {
        var context = TenantContextFor(TenantA, "tenant-a");
        await using var db = CreateDb(context);
        AddTenant(db, TenantA, "tenant-a");
        var card = AddCustomer(db, TenantA, "Ana", "A-7", 10);
        db.PointTransactions.Add(new PointTransaction(Guid.NewGuid(), TenantA, card, 30, TransactionType.Purchase, "Dentro", Now.AddMonths(-1), purchaseAmount: 300m));
        db.PointTransactions.Add(new PointTransaction(Guid.NewGuid(), TenantA, card, 999, TransactionType.Purchase, "Fuera", Now.AddMonths(-8), purchaseAmount: 9999m));
        await db.SaveChangesAsync();

        var report = await Service(db, context).GetActivityTrendsAsync(new(Now.AddMonths(-3), Now.AddDays(1)));

        Assert.Equal(30, report.Periods.Sum(x => x.PointsIssued));
        Assert.Equal(300m, report.Periods.Sum(x => x.PurchaseAmount));
        Assert.Contains(report.Periods, x => x.ActiveCustomers == 0);
    }
    private static ReportsReadService Service(AppDbContext db, TenantContext context) => new(db, context, new FixedClock(Now));
    private static TenantContext TenantContextFor(Guid id, string slug) { var context = new TenantContext(); context.SetTenant(id, slug); return context; }
    private static AppDbContext CreateDb(TenantContext context, string? name = null) => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name ?? "AdvancedReports-" + Guid.NewGuid().ToString("N")).Options, Mock.Of<IPublisher>(), context);
    private static void AddTenant(AppDbContext db, Guid id, string slug) => db.Tenants.Add(new Tenant(id, slug, slug, "America/Tijuana", Now.AddYears(-1)));
    private static Guid AddCustomer(AppDbContext db, Guid tenantId, string name, string serial, int points)
    {
        var customerId = Guid.NewGuid(); var cardId = Guid.NewGuid();
        db.Customers.Add(new Customer(customerId, tenantId, name, $"{serial}@example.test", Customer.BirthdayNotCaptured, Now.AddDays(-100), "6460000000"));
        var card = new LoyaltyCard(cardId, tenantId, customerId, serial, Now.AddDays(-100));
        if (points > 0) { card.EarnPoints(points, TransactionType.BonusWelcome, LoyaltyCloud.Domain.ValueObjects.ProgramConfigSnapshot.FromEntries([]), new FixedClock(Now.AddDays(-100))); card.ClearDomainEvents(); }
        db.LoyaltyCards.Add(card); return cardId;
    }
    private sealed class FixedClock(DateTime now) : IDateTimeProvider { public DateTime UtcNow { get; } = now; public DateTime Today => UtcNow.Date; }
}