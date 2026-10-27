using System.Net;
using System.Net.Http.Json;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Common.Wallet;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Persistence.Seed;
using LoyaltyCloud.Infrastructure.Services.GoogleWallet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class GoogleStampWalletIntegrationTests : IntegrationTestBase
{
    private readonly string Serial = $"ST-{Guid.NewGuid():N}"[..18].ToUpperInvariant();
    private readonly string TenantSlug = $"google-stamps-{Guid.NewGuid():N}";
    private readonly Guid TenantId = Guid.NewGuid();

    public GoogleStampWalletIntegrationTests(CustomWebApplicationFactory factory) : base(factory) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        Factory.GoogleWallet.StampClasses.Clear();
        Factory.GoogleWallet.StampObjects.Clear();
        Factory.GoogleWallet.StampObjectUpdateNotifications.Clear();
        Factory.GoogleWallet.FailingObjectId = null;
        Factory.GoogleWallet.BeforeStampObjectUpdateAsync = null;
        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMutableTenantContext>()
            .SetTenant(TenantId, TenantSlug);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var tenant = new Tenant(TenantId, TenantSlug, "Google Stamps", "America/Tijuana", now,
            programType: LoyaltyProgramType.Stamps);
        var subscription = new TenantSubscription(TenantId, TenantSubscriptionStatus.Active, "internal",
            paidThroughUtc: now.AddDays(30));
        var tenantBranding = new TenantBranding(TenantId, primaryColor: "#234567", secondaryColor: "#F0B429");
        tenantBranding.SetWalletBackgroundColor("#234567");
        var customer = new Customer(Guid.NewGuid(), TenantId, "Ana Sellos", "ana-stamps@test.local",
            Customer.BirthdayNotCaptured, now);
        var card = new LoyaltyCard(Guid.NewGuid(), TenantId, customer.Id, Serial, now);
        var program = new StampProgramVersion(Guid.NewGuid(), TenantId, 1, 10, 2,
            StampReversalPolicy.AdminAndCashier, Guid.NewGuid(), now);
        var identity = new StampRewardIdentity(Guid.NewGuid(), TenantId);
        var reward = new StampRewardDefinition(Guid.NewGuid(), program, 10, "Bebida gratis", null,
            identity.Id, "Bebida");
        var cycle = new StampCycle(Guid.NewGuid(), card, program, now);
        db.AddRange(tenant, subscription, tenantBranding, customer, card, program, identity, reward, cycle);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Save_link_creates_stamps_class_object_once_and_subsequent_sync_keeps_ids()
    {
        var response = await Client.PostAsync($"/api/customers/{Serial}/wallets/google/save-link", null);
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Status={response.StatusCode}; Body={responseBody}");
        var save = await response.Content.ReadFromJsonAsync<GoogleWalletSaveLinkResponse>();
        Assert.NotNull(save);
        Assert.Contains("-stamps-", save!.ClassId);
        Assert.Contains("-stamps-", save.ObjectId);
        var created = Assert.Single(Factory.GoogleWallet.StampObjects);
        Assert.Equal(2, created.CurrentStamps);
        Assert.Equal(10, created.MaxStamps);
        Assert.NotNull(created.ProgressImageUri);
        Assert.Empty(Factory.GoogleWallet.Objects);

        var progressUri = new Uri(created.ProgressImageUri!);
        var progressResponse = await Client.GetAsync(progressUri.PathAndQuery);
        Assert.Equal(HttpStatusCode.OK, progressResponse.StatusCode);
        Assert.Equal("image/png", progressResponse.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await progressResponse.Content.ReadAsByteArrayAsync());

        using (var scope = Factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>()
                .SetTenant(TenantId, TenantSlug);
            await scope.ServiceProvider.GetRequiredService<IGoogleWalletService>()
                .SynchronizeBySerialNumberIfExistsAsync(Serial);
        }

        Assert.Equal(2, Factory.GoogleWallet.StampObjects.Count);
        Assert.All(Factory.GoogleWallet.StampObjects, x => Assert.Equal(save.ObjectId, x.Id));
        Assert.True(Factory.GoogleWallet.StampObjectUpdateNotifications.Last());
        using var verify = Factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Single(await db.MemberDigitalWallets.IgnoreQueryFilters()
            .Where(x => x.TenantId == TenantId && x.Provider == DigitalWalletProvider.Google).ToListAsync());
    }

    [Fact]
    public async Task Failed_update_is_recorded_and_a_later_sync_recovers_the_same_object()
    {
        var response = await Client.PostAsync($"/api/customers/{Serial}/wallets/google/save-link", null);
        response.EnsureSuccessStatusCode();
        var save = await response.Content.ReadFromJsonAsync<GoogleWalletSaveLinkResponse>();
        Assert.NotNull(save);

        Factory.GoogleWallet.FailingObjectId = save!.ObjectId;
        await SynchronizeAsync();

        using (var failedScope = Factory.Services.CreateScope())
        {
            failedScope.ServiceProvider.GetRequiredService<IMutableTenantContext>()
                .SetTenant(TenantId, TenantSlug);
            var failedDb = failedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var wallet = await failedDb.MemberDigitalWallets.SingleAsync(x => x.ExternalObjectId == save.ObjectId);
            Assert.Equal(DigitalWalletStatus.Error, wallet.Status);
            Assert.NotNull(wallet.LastSynchronizationError);
        }

        Factory.GoogleWallet.FailingObjectId = null;
        await SynchronizeAsync();

        using var recoveredScope = Factory.Services.CreateScope();
        recoveredScope.ServiceProvider.GetRequiredService<IMutableTenantContext>()
            .SetTenant(TenantId, TenantSlug);
        var recoveredDb = recoveredScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var recovered = await recoveredDb.MemberDigitalWallets.SingleAsync(x => x.ExternalObjectId == save.ObjectId);
        Assert.Equal(DigitalWalletStatus.Active, recovered.Status);
        Assert.Null(recovered.LastSynchronizationError);
        Assert.All(Factory.GoogleWallet.StampObjects, x => Assert.Equal(save.ObjectId, x.Id));
    }

    [Fact]
    public async Task Concurrent_change_is_detected_and_the_latest_projection_wins()
    {
        var response = await Client.PostAsync($"/api/customers/{Serial}/wallets/google/save-link", null);
        response.EnsureSuccessStatusCode();
        var initial = Assert.Single(Factory.GoogleWallet.StampObjects);
        var changed = false;
        Factory.GoogleWallet.BeforeStampObjectUpdateAsync = async _ =>
        {
            if (changed) return;
            changed = true;
            using var mutationScope = Factory.Services.CreateScope();
            mutationScope.ServiceProvider.GetRequiredService<IMutableTenantContext>()
                .SetTenant(TenantId, TenantSlug);
            var mutationDb = mutationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cycle = await mutationDb.StampCycles.SingleAsync(x => x.LoyaltyCardSerialNumber == Serial);
            var program = await mutationDb.StampProgramVersions.SingleAsync(x => x.Id == cycle.StampProgramVersionId);
            cycle.Earn(program, DateTime.UtcNow);
            await mutationDb.SaveChangesAsync();
        };

        await SynchronizeAsync();

        Assert.True(changed);
        Assert.Equal(3, Factory.GoogleWallet.StampObjects.Last().CurrentStamps);
        Assert.NotEqual(initial.ProgressImageUri, Factory.GoogleWallet.StampObjects.Last().ProgressImageUri);
        Assert.Equal(3, Factory.GoogleWallet.StampObjects.Count);
    }

    [Fact]
    public async Task Branding_change_refreshes_existing_stamp_object_and_changes_rendered_asset_url()
    {
        var response = await Client.PostAsync($"/api/customers/{Serial}/wallets/google/save-link", null);
        response.EnsureSuccessStatusCode();
        var initial = Assert.Single(Factory.GoogleWallet.StampObjects);

        using (var updateScope = Factory.Services.CreateScope())
        {
            updateScope.ServiceProvider.GetRequiredService<IMutableTenantContext>()
                .SetTenant(TenantId, TenantSlug);
            var updateDb = updateScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenantBranding = await updateDb.TenantBrandings.SingleAsync(x => x.TenantId == TenantId);
            tenantBranding.SetWalletBackgroundColor("#765432");
            await updateDb.SaveChangesAsync();
        }

        using (var syncScope = Factory.Services.CreateScope())
        {
            syncScope.ServiceProvider.GetRequiredService<IMutableTenantContext>()
                .SetTenant(TenantId, TenantSlug);
            var synchronized = await syncScope.ServiceProvider
                .GetRequiredService<GoogleWalletBrandingSynchronizer>()
                .RefreshAsync(TenantId, default);
            Assert.True(synchronized);
        }

        var updated = Factory.GoogleWallet.StampObjects.Last();
        Assert.NotEqual(initial.ProgressImageUri, updated.ProgressImageUri);
        Assert.Equal("#765432", Factory.GoogleWallet.StampClasses.Last().HexBackgroundColor);
    }

    private async Task SynchronizeAsync()
    {
        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMutableTenantContext>()
            .SetTenant(TenantId, TenantSlug);
        await scope.ServiceProvider.GetRequiredService<IGoogleWalletService>()
            .SynchronizeBySerialNumberIfExistsAsync(Serial);
    }
}
