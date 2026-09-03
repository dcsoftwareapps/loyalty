using System.Text.Json;
using System.Text.Json.Nodes;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.Persistence;
using LoyaltyCloud.Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LoyaltyCloud.Tests.Infrastructure;

public sealed class GiftCardAppleWalletServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("b1000000-0000-0000-0000-000000000001");
    private static readonly Guid UserId = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    private static readonly DateTime Now = new(2026, 9, 1, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Category", "GiftCards")]
    [Trait("Category", "TenantBranding")]
    [Trait("Category", "WalletProductionUpdate")]
    public async Task Apple_gift_card_pass_uses_tenant_wallet_branding_colors_and_assets()
    {
        await using var db = CreateContext();
        var card = await SeedAsync(db, expiresAtUtc: null, senderName: null);
        var package = new CapturingPassPackageBuilder();
        var assets = new Mock<ITenantWalletAssetProvider>();
        assets.Setup(x => x.LoadAssetsAsync(
                TenantId,
                "kbeauty",
                "tenant-branding/kbeauty/wallet-branding/logo-original.png",
                "tenant-branding/kbeauty/logo-original.png",
                false,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new WalletPassAsset("logo.png", [1, 2, 3])]);

        await Service(db, package, assets.Object, Branding()).CreateOrUpdatePassAsync(card.Id);

        var pass = package.PassJson!;
        Assert.Equal("rgb(12,34,56)", pass["backgroundColor"]!.GetValue<string>());
        Assert.Equal("rgb(250,250,250)", pass["foregroundColor"]!.GetValue<string>());
        Assert.Equal("rgb(230,230,230)", pass["labelColor"]!.GetValue<string>());
        Assert.NotEqual("#1C1B18", pass["backgroundColor"]!.GetValue<string>());
        assets.VerifyAll();
    }

    [Fact]
    [Trait("Category", "GiftCards")]
    [Trait("Category", "WalletProductionUpdate")]
    public async Task Apple_gift_card_pass_with_expiration_shows_valid_until_and_recipient()
    {
        await using var db = CreateContext();
        var card = await SeedAsync(db, expiresAtUtc: new DateTime(2026, 12, 25, 0, 0, 0, DateTimeKind.Utc), senderName: "Daniel");
        var package = new CapturingPassPackageBuilder();

        await Service(db, package).CreateOrUpdatePassAsync(card.Id);

        var validUntil = SingleField(package.PassJson!, "valid_until");
        var recipient = SingleField(package.PassJson!, "recipient");
        Assert.Equal("VÁLIDA HASTA", validUntil["label"]!.GetValue<string>());
        Assert.Equal("25/12/2026", validUntil["value"]!.GetValue<string>());
        Assert.Equal("PARA", recipient["label"]!.GetValue<string>());
        Assert.Equal("Ana", recipient["value"]!.GetValue<string>());
        Assert.Equal(0, CountFields(package.PassJson!, "sender"));
        Assert.DoesNotContain("Sin expiración", package.PassJson!.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "GiftCards")]
    [Trait("Category", "WalletProductionUpdate")]
    public async Task Apple_gift_card_pass_without_expiration_with_sender_shows_sender_and_recipient()
    {
        await using var db = CreateContext();
        var card = await SeedAsync(db, expiresAtUtc: null, senderName: "Daniel");
        var package = new CapturingPassPackageBuilder();

        await Service(db, package).CreateOrUpdatePassAsync(card.Id);

        var sender = SingleField(package.PassJson!, "sender");
        var recipient = SingleField(package.PassJson!, "recipient");
        Assert.Equal("DE", sender["label"]!.GetValue<string>());
        Assert.Equal("Daniel", sender["value"]!.GetValue<string>());
        Assert.Equal("PARA", recipient["label"]!.GetValue<string>());
        Assert.Equal("Ana", recipient["value"]!.GetValue<string>());
        Assert.DoesNotContain("Sin expiración", package.PassJson!.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "GiftCards")]
    [Trait("Category", "WalletProductionUpdate")]
    public async Task Apple_gift_card_pass_without_expiration_or_sender_omits_filler_and_keeps_recipient()
    {
        await using var db = CreateContext();
        var card = await SeedAsync(db, expiresAtUtc: null, senderName: null);
        var package = new CapturingPassPackageBuilder();

        await Service(db, package).CreateOrUpdatePassAsync(card.Id);

        Assert.Equal(0, CountFields(package.PassJson!, "valid_until"));
        Assert.Equal(0, CountFields(package.PassJson!, "sender"));
        Assert.Equal("Ana", SingleField(package.PassJson!, "recipient")["value"]!.GetValue<string>());
        Assert.DoesNotContain("Sin expiración", package.PassJson!.ToJsonString(), StringComparison.Ordinal);
    }

    private static GiftCardAppleWalletService Service(
        AppDbContext db,
        CapturingPassPackageBuilder package,
        ITenantWalletAssetProvider? assets = null,
        TenantWalletBrandingDto? branding = null)
    {
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(x => x.UtcNow).Returns(Now);
        clock.SetupGet(x => x.Today).Returns(Now.Date);

        var brandingReader = new Mock<ITenantWalletBrandingReadService>();
        brandingReader.Setup(x => x.GetForTenantAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(branding ?? Branding());

        return new(
            db,
            new TestMutableTenantContext(TenantId, "kbeauty"),
            clock.Object,
            package,
            assets ?? EmptyAssets(),
            brandingReader.Object,
            new Mock<IApnService>().Object,
            Options.Create(new ApplePassOptions
            {
                PassTypeIdentifier = "pass.com.kbeautymx.loyalty",
                TeamIdentifier = "HS2XCFGQ75",
                WebServiceURL = "https://api.example.test",
                OrganizationName = "KBeauty"
            }));
    }

    private static TenantWalletBrandingDto Branding() => new(
        TenantId,
        "kbeauty",
        "KBeauty",
        "KBeauty",
        "KBeauty Loyalty",
        "rgb(12,34,56)",
        "rgb(250,250,250)",
        "rgb(230,230,230)",
        "#0C2238",
        "tenant-branding/kbeauty/logo-original.png",
        "tenant-branding/kbeauty/wallet-branding/logo-original.png",
        100,
        "CustomerName",
        null,
        "@kbeauty",
        "Cliente KBeauty",
        UsesBundledAssetsFallback: false,
        UsesLegacyContactFallback: false);

    private static ITenantWalletAssetProvider EmptyAssets()
    {
        var assets = new Mock<ITenantWalletAssetProvider>();
        assets.Setup(x => x.LoadAssetsAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        return assets.Object;
    }

    private static async Task<GiftCard> SeedAsync(AppDbContext db, DateTime? expiresAtUtc, string? senderName)
    {
        db.Tenants.Add(new Tenant(TenantId, "kbeauty", "KBeauty", "America/Tijuana", Now));
        var config = new GiftCardConfiguration(Guid.NewGuid(), TenantId, Now);
        config.Update(
            enabled: true,
            custom: true,
            partial: true,
            promotional: false,
            expirationMode: GiftCardExpirationMode.Never,
            months: null,
            currency: "MXN",
            displayName: "Gift Card",
            primaryColor: "#1C1B18",
            textColor: "#FFFFFF",
            logoUrl: null,
            secondaryText: null,
            terms: null,
            footer: null,
            nowUtc: Now);
        var card = new GiftCard(
            Guid.NewGuid(),
            TenantId,
            "GC-AAAA-BBBB-CCCC",
            GiftCard.HashClaimToken("claim-token"),
            500m,
            "MXN",
            null,
            "Ana",
            null,
            null,
            senderName,
            null,
            GiftCardSource.Manual,
            UserId,
            Now,
            expiresAtUtc);
        db.AddRange(config, card);
        await db.SaveChangesAsync();
        return card;
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new(options, new Mock<IPublisher>().Object, new TestMutableTenantContext(TenantId, "kbeauty"));
    }

    private static JsonObject SingleField(JsonObject passJson, string key)
    {
        var fields = AllFields(passJson)
            .Where(field => string.Equals(field["key"]?.GetValue<string>(), key, StringComparison.Ordinal))
            .ToArray();
        return Assert.Single(fields);
    }

    private static int CountFields(JsonObject passJson, string key) =>
        AllFields(passJson).Count(field => string.Equals(field["key"]?.GetValue<string>(), key, StringComparison.Ordinal));

    private static IEnumerable<JsonObject> AllFields(JsonObject passJson)
    {
        var storeCard = passJson["storeCard"]!.AsObject();
        foreach (var fieldGroup in new[] { "primaryFields", "secondaryFields", "auxiliaryFields", "backFields" })
        {
            foreach (var field in storeCard[fieldGroup]!.AsArray())
                yield return field!.AsObject();
        }
    }

    private sealed class CapturingPassPackageBuilder : IApplePassPackageBuilder
    {
        public JsonObject? PassJson { get; private set; }

        public Task<byte[]> BuildAsync(byte[] passJson, IReadOnlyList<WalletPassAsset> assets, CancellationToken ct = default)
        {
            PassJson = JsonNode.Parse(passJson)!.AsObject();
            return Task.FromResult(passJson);
        }
    }

    private sealed class TestMutableTenantContext(Guid tenantId, string tenantSlug) : IMutableTenantContext
    {
        public Guid? TenantId { get; private set; } = tenantId;
        public string? TenantSlug { get; private set; } = tenantSlug;
        public bool HasTenant => TenantId is not null;
        public void SetTenant(Guid tenantId, string tenantSlug) { TenantId = tenantId; TenantSlug = tenantSlug; }
        public void Clear() { TenantId = null; TenantSlug = null; }
    }
}
