using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Stamps;
using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.Services.GoogleWallet;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace LoyaltyCloud.Tests.Infrastructure;

public sealed class GoogleStampWalletMapperTests
{
    private readonly GoogleStampWalletMapper mapper = new();

    [Theory]
    [InlineData(0, 10, "0 de 10 sellos", "10 sellos")]
    [InlineData(4, 10, "4 de 10 sellos", "6 sellos")]
    [InlineData(10, 10, "10 de 10 sellos", "Tarjeta completa")]
    public void Object_maps_zero_partial_and_complete_progress(int current, int max, string progress, string remaining)
    {
        var value = mapper.ToObjectData("issuer.object", "issuer.class", "Ana Lopez", "LC-001", true,
            DateTime.UnixEpoch, Projection(current, max), "https://api.example/progress.png?v=1");
        var payload = mapper.ToObjectPayload(value);

        Assert.Equal(progress, value.ProgressText);
        Assert.Equal(remaining, value.RemainingText);
        Assert.Equal("Ana", value.AccountName);
        Assert.True(payload.ContainsKey("heroImage"));
        var points = Assert.IsType<Dictionary<string, object?>>(payload["loyaltyPoints"]);
        Assert.Equal("SELLOS", points["label"]);
        Assert.Equal(current, Assert.IsType<Dictionary<string, object?>>(points["balance"])["int"]);
    }

    [Fact]
    public void Object_maps_intermediate_final_available_redeemed_and_previous_cycle_rewards()
    {
        var availableId = Guid.NewGuid();
        var projection = Projection(5, 10) with
        {
            CycleSequence = 2,
            Rewards =
            [
                new(5, Guid.NewGuid(), null, StampWalletRewardState.Available),
                new(10, Guid.NewGuid(), null, StampWalletRewardState.Upcoming)
            ],
            EarnedRewards =
            [
                new(availableId, "Cafe", "Cafe", 5, StampWalletRewardState.Available, 2),
                new(Guid.NewGuid(), "Postre", "Postre", 10, StampWalletRewardState.Available, 1),
                new(Guid.NewGuid(), "Bebida", "Bebida", 5, StampWalletRewardState.Redeemed, 1)
            ]
        };

        var value = mapper.ToObjectData("object", "class", "Ana", "LC-1", true, DateTime.UnixEpoch, projection, null);

        Assert.Equal("Cafe, Postre", value.AvailableRewardsText);
        Assert.Equal("1 recompensa canjeada", value.RewardHistoryText);
        Assert.Equal("En el sello 10", value.NextRewardText);
        Assert.Equal(2, value.CycleSequence);
    }

    [Fact]
    public void Class_is_tenant_branded_and_has_stamps_template_without_points_fields()
    {
        var branding = Branding();
        var data = mapper.ToClassData("issuer.stamps", new GoogleWalletOptions
        {
            LogoUri = "https://api.loyaltycloud.net/logo.png"
        }, branding);
        var payload = mapper.ToClassPayload(data);
        var json = System.Text.Json.JsonSerializer.Serialize(payload);

        Assert.Equal(branding.DisplayName, payload["programName"]);
        Assert.Contains("stamps-progress", json);
        Assert.DoesNotContain("remaining-points", json);
        Assert.DoesNotContain("next-level", json);
    }

    [Fact]
    public void Stamp_ids_are_deterministic_tenant_aware_and_separate_from_points()
    {
        var ids = new GoogleWalletIdGenerator();
        var options = new GoogleWalletOptions { IssuerId = "issuer", ClassSuffix = "loyalty", ObjectIdPrefix = "member" };
        var tenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        Assert.Equal("issuer.loyalty-stamps-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", ids.BuildStampClassId(options, tenantA));
        Assert.Equal(ids.BuildStampObjectId(options, tenantA, "LC 001"), ids.BuildStampObjectId(options, tenantA, "LC 001"));
        Assert.NotEqual(ids.BuildObjectId(options, tenantA, "LC 001"), ids.BuildStampObjectId(options, tenantA, "LC 001"));
        Assert.NotEqual(ids.BuildStampObjectId(options, tenantA, "LC 001"), ids.BuildStampObjectId(options, tenantB, "LC 001"));
    }

    [Fact]
    public void Progress_asset_token_is_opaque_tamper_resistant_and_round_trips_tenant()
    {
        var service = new GoogleStampWalletAssetTokenService(new EphemeralDataProtectionProvider());
        var tenantId = Guid.NewGuid();
        var token = service.Create(tenantId, "Tenant-A", "lc-001");

        Assert.DoesNotContain("LC-001", token, StringComparison.OrdinalIgnoreCase);
        Assert.True(service.TryRead(token, out var value));
        Assert.Equal(tenantId, value.TenantId);
        Assert.Equal("tenant-a", value.TenantSlug);
        Assert.Equal("LC-001", value.SerialNumber);
        Assert.False(service.TryRead(token + "x", out _));
    }

    [Fact]
    public void Enabled_configuration_requires_public_https_logo_and_ascii_identifiers()
    {
        var invalid = new GoogleWalletOptions
        {
            Enabled = true,
            IssuerId = "issuer",
            ClassSuffix = "sellos-ñ",
            ObjectIdPrefix = "member",
            LogoUri = "http://localhost/logo.png",
            ServiceAccountJson = "{}"
        };

        var errors = invalid.ValidateForEnabled();

        Assert.Contains(errors, x => x.Contains("ClassSuffix", StringComparison.Ordinal));
        Assert.Contains(errors, x => x.Contains("LogoUri", StringComparison.Ordinal));
    }

    [Fact]
    public void Stamp_identifiers_always_use_google_supported_ascii_characters()
    {
        var ids = new GoogleWalletIdGenerator();
        var options = new GoogleWalletOptions
        {
            IssuerId = "issuer",
            ClassSuffix = "loyalty",
            ObjectIdPrefix = "member"
        };

        var objectId = ids.BuildStampObjectId(options, Guid.NewGuid(), "TARJETA-Ñ-01");

        Assert.Matches("^[A-Za-z0-9._-]+$", objectId);
        Assert.DoesNotContain("ñ", objectId, StringComparison.OrdinalIgnoreCase);
    }

    private static StampWalletProjection Projection(int current, int max) =>
        new(Guid.NewGuid(), Guid.NewGuid(), current, max, null, null,
        [new(max, Guid.NewGuid(), null, current == max ? StampWalletRewardState.Available : StampWalletRewardState.Upcoming)]);

    private static TenantWalletBrandingDto Branding() => new(
        Guid.Parse("b1000000-0000-0000-0000-000000000001"), "tenant", "Cafe Sellos", "Cafe",
        "Sellos", "rgb(255,255,255)", "rgb(0,0,0)", "rgb(0,0,0)", "#FFFFFF",
        null, null, 100, "CustomerName", null, "Cafe", "Cliente", false, false);
}
