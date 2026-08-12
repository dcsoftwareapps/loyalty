using LoyaltyCloud.Application.Common.Wallet;
using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.Services.GoogleWallet;
using Xunit;

namespace LoyaltyCloud.Tests.Infrastructure;

public sealed class GoogleWalletObjectMapperTests
{
    [Fact]
    public void ToObjectPayload_ShouldMapPointsTierAndBarcode()
    {
        var mapper = new GoogleWalletObjectMapper();
        var member = new MemberWalletData(
            TenantId: Guid.Parse("b1000000-0000-0000-0000-000000000001"),
            CustomerId: Guid.NewGuid(),
            LoyaltyCardId: Guid.NewGuid(),
            SerialNumber: "KB-123",
            FullName: "Ana Lopez",
            Email: "ana@test.local",
            Phone: null,
            CurrentPoints: 250,
            LifetimePoints: 500,
            Level: "Glow",
            LevelAchievedAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            LastActivityAt: new DateTime(2026, 7, 15, 12, 30, 0, DateTimeKind.Utc),
            IsActive: true,
            BarcodeValue: "KB-123",
            DisplayName: "Ana",
            PointsText: "250 pts",
            LevelText: "Glow \u2728",
            NextLevelText: "Radiance",
            RemainingPointsText: "2750 pts",
            BarcodeAlternateText: "Presenta este c\u00f3digo en caja");

        var data = mapper.ToObjectData("issuer.member-kb-123", "issuer.loyalty", member);
        var payload = mapper.ToObjectPayload(data);

        Assert.Equal("issuer.member-kb-123", payload["id"]);
        Assert.Equal("issuer.loyalty", payload["classId"]);
        Assert.Equal("ACTIVE", payload["state"]);
        Assert.Equal("Ana", payload["accountName"]);
        Assert.Equal("KB-123", payload["accountId"]);

        var points = Assert.IsType<Dictionary<string, object?>>(payload["loyaltyPoints"]);
        Assert.Equal("PUNTOS", points["label"]);
        var balance = Assert.IsType<Dictionary<string, object?>>(points["balance"]);
        Assert.Equal(250, balance["int"]);

        var barcode = Assert.IsType<Dictionary<string, object?>>(payload["barcode"]);
        Assert.Equal("QR_CODE", barcode["type"]);
        Assert.Equal("KB-123", barcode["value"]);
        Assert.Equal("Presenta este c\u00f3digo en caja", barcode["alternateText"]);

        var textModules = Assert.IsType<Dictionary<string, object?>[]>(payload["textModulesData"]);
        Assert.Contains(textModules, module => Equals(module["id"], "member-name") && Equals(module["body"], "Ana"));
        Assert.Contains(textModules, module => Equals(module["id"], "points") && Equals(module["body"], "250 pts"));
        Assert.Contains(textModules, module => Equals(module["id"], "level") && Equals(module["body"], "Glow \u2728"));
        Assert.Contains(textModules, module => Equals(module["id"], "next-level") && Equals(module["body"], "Radiance"));
        Assert.Contains(textModules, module => Equals(module["id"], "remaining-points") && Equals(module["body"], "2750 pts"));
        Assert.Contains(textModules, module => Equals(module["id"], "barcode-caption") && Equals(module["body"], "Presenta este c\u00f3digo en caja"));
        Assert.Contains(textModules, module => Equals(module["body"], "2026-07-15 12:30 UTC"));
    }

    [Fact]
    public void ToClassPayload_ShouldIncludeBrandingOnlyWhenConfigured()
    {
        var mapper = new GoogleWalletObjectMapper();
        var options = new GoogleWalletOptions
        {
            ProgramName = "KBeauty Loyalty",
            IssuerName = "KBeauty MX",
            LogoUri = "https://assets.example/logo.png",
            HexBackgroundColor = "#FFFFFF"
        };

        var data = mapper.ToClassData("issuer.loyalty", options);
        var payload = mapper.ToClassPayload(data);

        Assert.Equal("issuer.loyalty", payload["id"]);
        Assert.Equal("KBeauty Loyalty", payload["programName"]);
        Assert.Equal("KBeauty MX", payload["issuerName"]);
        Assert.Equal("#FFFFFF", payload["hexBackgroundColor"]);
        Assert.True(payload.ContainsKey("programLogo"));
        Assert.False(payload.ContainsKey("heroImage"));
        Assert.True(payload.ContainsKey("classTemplateInfo"));
    }

    [Fact]
    public void ToClassPayload_ShouldUseUnderReviewAndNeverApproved()
    {
        var mapper = new GoogleWalletObjectMapper();
        var options = new GoogleWalletOptions
        {
            ProgramName = "KBeauty Loyalty",
            IssuerName = "KBeauty MX",
            LogoUri = "https://assets.example/logo.png",
            HexBackgroundColor = "#FFFFFF"
        };

        var data = mapper.ToClassData("issuer.loyalty", options);
        var payload = mapper.ToClassPayload(data);

        Assert.Equal("UNDER_REVIEW", payload["reviewStatus"]);
        Assert.DoesNotContain(payload, item => Equals(item.Value, "APPROVED"));
    }
}

