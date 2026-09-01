using System.Net;
using System.Net.Http.Json;
using LoyaltyCloud.Application.Admin.Queries.GetAdminDashboard;
using LoyaltyCloud.Application.Admin.Queries.GetReportsSummary;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Customers.Commands.DeleteCustomer;
using LoyaltyCloud.Application.Customers.Queries.GetCustomerBySerial;
using LoyaltyCloud.Application.Customers.Queries.GetCustomerDetail;
using LoyaltyCloud.Application.Customers.Queries.GetCustomers;
using LoyaltyCloud.Application.Customers.Queries.GetCustomerTransactions;
using LoyaltyCloud.Application.Points.Commands.AddPoints;
using LoyaltyCloud.Application.Redemptions.Queries.GetRedemptionCatalog;
using LoyaltyCloud.Common.Pagination;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.ValueObjects;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class CustomerSoftDeleteTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private const string PassTypeIdentifier = "pass.com.kbeautymx.loyalty";
    private static readonly Guid OtherTenantId = Guid.Parse("d1000000-0000-0000-0000-000000000001");

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CustomerSoftDeleteTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseCreatedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Trait("Category", "CustomerSoftDelete")]
    public async Task DeleteCustomer_deactivates_customer_and_card_without_removing_history()
    {
        var member = await CreateMemberAsync(currentPoints: 150);

        var result = await SendAsTenantAsync(new DeleteCustomerCommand(member.CustomerId));

        Assert.True(result.IsSuccess);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var customer = await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == member.CustomerId);
        var card = await db.LoyaltyCards.IgnoreQueryFilters().SingleAsync(c => c.Id == member.CardId);
        var transactions = await db.PointTransactions.IgnoreQueryFilters().CountAsync(t => t.LoyaltyCardId == member.CardId);

        Assert.False(customer.IsActive);
        Assert.False(card.IsActive);
        Assert.Equal(1, transactions);
    }

    [Fact]
    [Trait("Category", "CustomerSoftDelete")]
    [Trait("Category", "AdminCustomerPoints")]
    [Trait("Category", "AdminRedemptionFlow")]
    public async Task Deleted_customer_is_hidden_from_admin_reads_and_blocked_from_points_and_redemption()
    {
        var member = await CreateMemberAsync(currentPoints: 500);
        await SendAsTenantAsync(new DeleteCustomerCommand(member.CustomerId));

        var customers = await SendAsTenantAsync(new GetCustomersQuery(
            member.SerialNumber,
            LevelFilter: null,
            new PaginationParams { PageNumber = 1, PageSize = 20 }));
        var detail = await SendAsTenantAsync(new GetCustomerDetailQuery(member.CustomerId));
        var bySerial = await SendAsTenantAsync(new GetCustomerBySerialQuery(member.SerialNumber));
        var transactions = await SendAsTenantAsync(new GetCustomerTransactionsQuery(
            member.SerialNumber,
            new PaginationParams { PageNumber = 1, PageSize = 20 }));
        var catalog = await SendAsTenantAsync(new GetRedemptionCatalogQuery(member.SerialNumber));
        var addPoints = await SendAsTenantAsync(new AddPointsCommand(member.SerialNumber, 100m, "cashier"));

        Assert.True(customers.IsSuccess);
        Assert.Empty(customers.Value.Items);
        Assert.True(detail.IsFailure);
        Assert.True(bySerial.IsFailure);
        Assert.True(transactions.IsFailure);
        Assert.True(catalog.IsFailure);
        Assert.True(addPoints.IsFailure);
    }

    [Fact]
    [Trait("Category", "CustomerSoftDelete")]
    [Trait("Category", "PublicJoin")]
    [Trait("Category", "CustomerPhoneRecovery")]
    public async Task Deleted_customer_phone_does_not_create_duplicate_or_throw_on_public_join()
    {
        var member = await CreateMemberAsync(firstName: "Daniel", lastName: "Chavez");
        await SendAsTenantAsync(new DeleteCustomerCommand(member.CustomerId));

        using var response = await _client.PostAsJsonAsync("api/public/kbeauty/join", new
        {
            firstName = "Daniel",
            lastName = "Chavez",
            phone = member.Phone
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("No pudimos recuperar", body);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var customersWithPhone = await db.Customers
            .IgnoreQueryFilters()
            .CountAsync(c => c.TenantId == TenantSeed.KBeautyTenantId && c.NormalizedPhone == member.Phone);

        Assert.Equal(1, customersWithPhone);
    }

    [Fact]
    [Trait("Category", "CustomerSoftDelete")]
    [Trait("Category", "WalletProductionUpdate")]
    [Trait("Category", "Reports")]
    public async Task Deleted_customer_is_excluded_from_wallet_updates_and_current_reports()
    {
        var baselineReports = (await SendAsTenantAsync(new GetReportsSummaryQuery(
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow.AddDays(1),
            InactiveDaysThreshold: 90))).Value;
        var baselineDashboard = (await SendAsTenantAsync(new GetAdminDashboardQuery())).Value;

        var member = await CreateMemberAsync(currentPoints: 275);
        await AddWalletRecordsAsync(member);

        var resolverBeforeDelete = await ResolveWalletTenantAsync(member.SerialNumber);
        var registrationsBeforeDelete = await GetUpdatableSerialsAsync(member.SerialNumber);

        Assert.NotNull(resolverBeforeDelete);
        Assert.Contains(member.SerialNumber, registrationsBeforeDelete);

        await SendAsTenantAsync(new DeleteCustomerCommand(member.CustomerId));

        var resolverAfterDelete = await ResolveWalletTenantAsync(member.SerialNumber);
        var registrationsAfterDelete = await GetUpdatableSerialsAsync(member.SerialNumber);
        using var passResponse = await _client.GetAsync($"api/passes/{Uri.EscapeDataString(member.SerialNumber)}");
        using var googleResponse = await _client.PostAsync(
            $"api/customers/{Uri.EscapeDataString(member.SerialNumber)}/wallets/google/save-link",
            content: null);
        var reportsAfterDelete = (await SendAsTenantAsync(new GetReportsSummaryQuery(
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow.AddDays(1),
            InactiveDaysThreshold: 90))).Value;
        var dashboardAfterDelete = (await SendAsTenantAsync(new GetAdminDashboardQuery())).Value;

        Assert.Null(resolverAfterDelete);
        Assert.DoesNotContain(member.SerialNumber, registrationsAfterDelete);
        Assert.Equal(HttpStatusCode.NotFound, passResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, googleResponse.StatusCode);
        Assert.Equal(baselineReports.CurrentProgram.TotalCustomers, reportsAfterDelete.CurrentProgram.TotalCustomers);
        Assert.Equal(baselineReports.CurrentProgram.CurrentPointBalance, reportsAfterDelete.CurrentProgram.CurrentPointBalance);
        Assert.Equal(baselineReports.CurrentProgram.AppleWalletRegistrations, reportsAfterDelete.CurrentProgram.AppleWalletRegistrations);
        Assert.Equal(baselineReports.CurrentProgram.GoogleWalletRecords, reportsAfterDelete.CurrentProgram.GoogleWalletRecords);
        Assert.Equal(baselineDashboard.ActiveCustomersCount, dashboardAfterDelete.ActiveCustomersCount);
        Assert.DoesNotContain(dashboardAfterDelete.RecentVisits, visit => visit.SerialNumber == member.SerialNumber);
    }

    [Fact]
    [Trait("Category", "CustomerSoftDelete")]
    public async Task DeleteCustomer_is_tenant_scoped()
    {
        await EnsureTenantAsync(OtherTenantId, "other-soft-delete");
        var otherMember = await CreateMemberAsync(
            tenantId: OtherTenantId,
            tenantSlug: "other-soft-delete",
            firstName: "Otro",
            lastName: "Cliente");

        var result = await SendAsTenantAsync(new DeleteCustomerCommand(otherMember.CustomerId));

        Assert.True(result.IsFailure);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var otherCustomer = await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == otherMember.CustomerId);
        var otherCard = await db.LoyaltyCards.IgnoreQueryFilters().SingleAsync(c => c.Id == otherMember.CardId);

        Assert.True(otherCustomer.IsActive);
        Assert.True(otherCard.IsActive);
    }

    private async Task<MemberFixture> CreateMemberAsync(
        Guid? tenantId = null,
        string tenantSlug = TenantSeed.KBeautySlug,
        string firstName = "Cliente",
        string lastName = "SoftDelete",
        int currentPoints = 0)
    {
        var actualTenantId = tenantId ?? TenantSeed.KBeautyTenantId;
        var now = DateTime.UtcNow;
        var serial = $"KB-SD{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var phone = "646" + Random.Shared.Next(1000000, 9999999);
        var customerId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(actualTenantId, tenantSlug);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var customer = new Customer(
            customerId,
            actualTenantId,
            $"{firstName} {lastName}",
            $"{serial.ToLowerInvariant()}@example.test",
            Customer.BirthdayNotCaptured,
            now,
            phone);
        var card = new LoyaltyCard(cardId, actualTenantId, customerId, serial, now);

        if (currentPoints > 0)
        {
            card.EarnPoints(
                currentPoints,
                TransactionType.Purchase,
                ProgramConfigSnapshot.FromEntries([]),
                new FixedClock(now));
            card.ClearDomainEvents();
        }

        db.Customers.Add(customer);
        db.LoyaltyCards.Add(card);

        if (currentPoints > 0)
        {
            db.PointTransactions.Add(new PointTransaction(
                Guid.NewGuid(),
                actualTenantId,
                cardId,
                currentPoints,
                TransactionType.Purchase,
                "Compra de prueba",
                now,
                purchaseAmount: currentPoints * 10m));
        }

        await db.SaveChangesAsync();

        return new MemberFixture(actualTenantId, tenantSlug, customerId, cardId, serial, phone);
    }

    private async Task AddWalletRecordsAsync(MemberFixture member)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(member.TenantId, member.TenantSlug);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.DeviceRegistrations.Add(new DeviceRegistration(
            Guid.NewGuid(),
            member.TenantId,
            "device-soft-delete",
            PassTypeIdentifier,
            member.SerialNumber,
            "push-token",
            DateTime.UtcNow));

        var wallet = new MemberDigitalWallet(
            Guid.NewGuid(),
            member.TenantId,
            member.CustomerId,
            member.CardId,
            DigitalWalletProvider.Google,
            "issuer.loyalty",
            $"issuer.{member.SerialNumber}",
            DateTime.UtcNow);
        wallet.MarkSynchronized(DateTime.UtcNow);
        db.MemberDigitalWallets.Add(wallet);

        await db.SaveChangesAsync();
    }

    private async Task<WalletTenantInfo?> ResolveWalletTenantAsync(string serialNumber)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider
            .GetRequiredService<ILoyaltyCardTenantLookup>()
            .ResolveBySerialNumberAsync(serialNumber);
    }

    private async Task<IReadOnlyList<string>> GetUpdatableSerialsAsync(string serialNumber)
    {
        using var scope = _factory.Services.CreateScope();
        var result = await scope.ServiceProvider
            .GetRequiredService<IDeviceRegistrationPlatformReadService>()
            .GetUpdatableSerialsAsync("device-soft-delete", PassTypeIdentifier, passesUpdatedSince: null);

        return result.SerialNumbers;
    }

    private async Task EnsureTenantAsync(Guid tenantId, string slug)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(tenantId, slug);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == tenantId))
        {
            db.Tenants.Add(new Tenant(tenantId, slug, slug, "America/Tijuana", DateTime.UtcNow));
            db.TenantBrandings.Add(new TenantBranding(tenantId, "#111111", "#ffffff"));
            db.TenantSubscriptions.Add(new TenantSubscription(
                tenantId,
                TenantSubscriptionStatus.Active,
                "test",
                paidThroughUtc: DateTime.UtcNow.AddDays(30)));
            await db.SaveChangesAsync();
        }

        await IntegrationTestSeed.EnsureProgramConfigAsync(db, tenantId);
        await IntegrationTestSeed.EnsureDefaultTenantLevelsAsync(db, tenantId);
    }

    private async Task<TResponse> SendAsTenantAsync<TResponse>(IRequest<TResponse> request)
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider
            .GetRequiredService<IMutableTenantContext>()
            .SetTenant(TenantSeed.KBeautyTenantId, TenantSeed.KBeautySlug);
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private sealed record MemberFixture(
        Guid TenantId,
        string TenantSlug,
        Guid CustomerId,
        Guid CardId,
        string SerialNumber,
        string Phone);

    private sealed class FixedClock(DateTime now) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = now;
        public DateTime Today => UtcNow.Date;
    }
}
