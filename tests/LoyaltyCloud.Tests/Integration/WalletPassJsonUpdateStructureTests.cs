using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Common.Constants;
using LoyaltyCloud.Common.Services;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Domain.ValueObjects;
using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class WalletPassJsonUpdateStructureTests
{
    [Fact]
    [Trait("Category", "WalletProductionUpdate")]
    public void Pass_json_keeps_identity_and_points_key_stable_when_points_change()
    {
        var now = new DateTime(2026, 7, 23, 23, 2, 0, DateTimeKind.Utc);
        var customer = new Customer(
            Guid.NewGuid(),
            Guid.Parse("b1000000-0000-0000-0000-000000000001"),
            "Daniel Chavez",
            "daniel@example.local",
            new DateTime(1990, 1, 1),
            now,
            "6461234567");
        var card = new LoyaltyCard(
            Guid.NewGuid(),
            Guid.Parse("b1000000-0000-0000-0000-000000000001"),
            customer.Id,
            "KB-LNB7ACG",
            now);
        var snapshot = ProgramConfigSnapshot.FromEntries([]);

        card.EarnPoints(195, TransactionType.Purchase, snapshot, new FixedClock(now));
        var passA = BuildPassJson(card, customer);

        card.EarnPoints(10, TransactionType.Purchase, snapshot, new FixedClock(now.AddMinutes(5)));
        var passB = BuildPassJson(card, customer);

        Assert.Equal("KB-LNB7ACG", passA["serialNumber"]!.GetValue<string>());
        Assert.Equal(passA["serialNumber"]!.GetValue<string>(), passB["serialNumber"]!.GetValue<string>());
        Assert.Equal("pass.com.kbeautymx.loyalty", passA["passTypeIdentifier"]!.GetValue<string>());
        Assert.Equal(passA["passTypeIdentifier"]!.GetValue<string>(), passB["passTypeIdentifier"]!.GetValue<string>());
        Assert.Equal(passA["authenticationToken"]!.GetValue<string>(), passB["authenticationToken"]!.GetValue<string>());
        Assert.Equal("https://loyaltycloud-api-894839.azurewebsites.net", passB["webServiceURL"]!.GetValue<string>());
        Assert.Equal(passA["webServiceURL"]!.GetValue<string>(), passB["webServiceURL"]!.GetValue<string>());
        Assert.Equal(passA["organizationName"]!.GetValue<string>(), passB["organizationName"]!.GetValue<string>());
        Assert.Equal(passA["formatVersion"]!.GetValue<int>(), passB["formatVersion"]!.GetValue<int>());
        Assert.Null(passB["logoText"]);
        Assert.Equal("rgb(255,255,255)", passB["backgroundColor"]!.GetValue<string>());
        Assert.Equal("rgb(0,0,0)", passB["foregroundColor"]!.GetValue<string>());
        Assert.Equal("rgb(28,28,28)", passB["labelColor"]!.GetValue<string>());

        var pointsA = SingleField(passA, "points");
        var pointsB = SingleField(passB, "points");
        Assert.Equal("points", pointsA["key"]!.GetValue<string>());
        Assert.Equal("PUNTOS", pointsA["label"]!.GetValue<string>());
        Assert.Equal("195 pts", pointsA["value"]!.GetValue<string>());
        Assert.Null(pointsA["changeMessage"]);
        Assert.Equal("points", pointsB["key"]!.GetValue<string>());
        Assert.Equal("PUNTOS", pointsB["label"]!.GetValue<string>());
        Assert.Equal("205 pts", pointsB["value"]!.GetValue<string>());
        Assert.Null(pointsB["changeMessage"]);

        Assert.Equal(1, CountFields(passA, "points"));
        Assert.Equal(1, CountFields(passB, "points"));
        Assert.Equal(0, CountFields(passA, "points_added"));
        Assert.Equal(0, CountFields(passB, "points_added"));
        Assert.DoesNotContain("195 pts", passB.ToJsonString(), StringComparison.Ordinal);
        Assert.DoesNotContain("205 pts", passA.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "WalletProductionUpdate")]
    public void Points_added_recent_event_adds_change_message_to_temporary_field_only()
    {
        var now = new DateTime(2026, 7, 23, 23, 2, 0, DateTimeKind.Utc);
        var customer = NewCustomer(now);
        var card = NewCard(customer.Id, now);
        var snapshot = ProgramConfigSnapshot.FromEntries([]);
        card.EarnPoints(225, TransactionType.Purchase, snapshot, new FixedClock(now));

        var notificationId = Guid.NewGuid();
        var context = new WalletNotificationContext(
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            new WalletPointsAddedMessage(notificationId, 10, 225, "10 puntos", "\ud83c\udf89 Sumaste %@"),
            new WalletRecentVisibleEvent(notificationId, NotificationType.PointsAdded, now, now, now.AddHours(24)));

        var pass = BuildPassJson(card, customer, context);
        var points = SingleField(pass, "points");
        var pointsAdded = SingleField(pass, "points_added");

        Assert.Equal("225 pts", points["value"]!.GetValue<string>());
        Assert.Null(points["changeMessage"]);
        Assert.Equal("SUMASTE", pointsAdded["label"]!.GetValue<string>());
        Assert.Equal("10 puntos", pointsAdded["value"]!.GetValue<string>());
        Assert.Equal("\ud83c\udf89 Sumaste %@", pointsAdded["changeMessage"]!.GetValue<string>());
        Assert.Equal(1, CountFields(pass, "points"));
        Assert.Equal(1, CountFields(pass, "points_added"));
    }

    [Fact]
    [Trait("Category", "WalletProductionUpdate")]
    public void Level_changed_recent_event_keeps_priority_over_points_added()
    {
        var now = new DateTime(2026, 7, 23, 23, 2, 0, DateTimeKind.Utc);
        var customer = NewCustomer(now);
        var card = NewCard(customer.Id, now);
        var snapshot = ProgramConfigSnapshot.FromEntries([]);
        card.EarnPoints(600, TransactionType.Purchase, snapshot, new FixedClock(now));

        var levelNotificationId = Guid.NewGuid();
        var pointsNotificationId = Guid.NewGuid();
        var context = new WalletNotificationContext(
            null,
            new WalletNotificationMessage(levelNotificationId, NotificationType.LevelChanged, "Subiste de nivel!", "Ahora eres cliente Glow", null),
            null,
            null,
            null,
            null,
            null,
            new WalletPointsAddedMessage(pointsNotificationId, 100, 600, "100 puntos", "\ud83c\udf89 Sumaste %@"),
            new WalletRecentVisibleEvent(levelNotificationId, NotificationType.LevelChanged, now, now, now.AddDays(7)));

        var pass = BuildPassJson(card, customer, context);
        var points = SingleField(pass, "points");

        Assert.Equal("600 pts", points["value"]!.GetValue<string>());
        Assert.Null(points["changeMessage"]);
        Assert.Equal(0, CountFields(pass, "points_added"));
    }

    [Fact]
    [Trait("Category", "WalletProductionUpdate")]
    public void Custom_message_uses_short_notification_and_long_detail()
    {
        var now = new DateTime(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);
        var customer = NewCustomer(now);
        var card = NewCard(customer.Id, now);
        var snapshot = ProgramConfigSnapshot.FromEntries([]);
        card.EarnPoints(225, TransactionType.Purchase, snapshot, new FixedClock(now));

        var notificationId = Guid.NewGuid();
        var context = new WalletNotificationContext(
            null,
            null,
            null,
            null,
            null,
            null,
            new WalletCustomMessage(
                notificationId,
                "NOVEDAD",
                "Brillitos hoy",
                "Hoy tenemos brillitos de regalo al visitar la tienda.",
                now.AddDays(2),
                "\ud83d\udce3 %@"),
            null,
            new WalletRecentVisibleEvent(notificationId, NotificationType.Custom, now, now, now.AddDays(2)));

        var pass = BuildPassJson(card, customer, context);
        var frontMessage = SingleField(pass, "custom_message");
        var detail = SingleField(pass, "custom_message_detail");

        Assert.Equal("NOVEDAD", frontMessage["label"]!.GetValue<string>());
        Assert.Equal("Brillitos hoy", frontMessage["value"]!.GetValue<string>());
        Assert.Equal("\ud83d\udce3 %@", frontMessage["changeMessage"]!.GetValue<string>());
        Assert.Equal("NOVEDAD\n\nHoy tenemos brillitos de regalo al visitar la tienda.", detail["value"]!.GetValue<string>());
    }

    [Fact]
    [Trait("Category", "MTLevel4")]
    public void Pass_json_uses_dynamic_next_and_remaining_level_fields()
    {
        var now = new DateTime(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);
        var customer = NewCustomer(now);
        var card = NewCard(customer.Id, now);
        var snapshot = ProgramConfigSnapshot.FromEntries([]);
        card.EarnPoints(205, TransactionType.Purchase, snapshot, new FixedClock(now));

        var pass = BuildPassJson(
            card,
            customer,
            progress: new PassProgressValues("Oro Member ✨", "Oro ✨", "205 pts", "Platino", "1,300 pts"));

        Assert.Equal("205 pts", SingleField(pass, "points")["value"]!.GetValue<string>());
        Assert.Equal("Oro ✨", SingleField(pass, "level")["value"]!.GetValue<string>());
        Assert.Equal("Platino", SingleField(pass, "next")["value"]!.GetValue<string>());
        Assert.Equal("1,300 pts", SingleField(pass, "remaining")["value"]!.GetValue<string>());
    }

    [Fact]
    [Trait("Category", "TenantBranding")]
    [Trait("Category", "WalletProductionUpdate")]
    public void Pass_json_uses_resolved_tenant_wallet_card_colors()
    {
        var now = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);
        var customer = NewCustomer(now);
        var card = NewCard(customer.Id, now);
        card.EarnPoints(205, TransactionType.Purchase, ProgramConfigSnapshot.FromEntries([]), new FixedClock(now));
        var pass = BuildPassJson(
            card,
            customer,
            branding: new TenantWalletBrandingDto(
                card.TenantId,
                "kbeauty",
                "KBeauty",
                "KBeauty MX",
                "KBeauty Loyalty",
                "rgb(28,28,28)",
                "rgb(255,255,255)",
                "rgb(255,255,255)",
                "#1C1C1C",
                "tenant-branding/test/wallet/logo-original.png",
                "tenant-branding/test/wallet-branding/logo-original.png",
                "@kbeauty_mx",
                "Cliente K-Beauty",
                UsesBundledAssetsFallback: false,
                UsesLegacyContactFallback: false));

        Assert.Equal("rgb(28,28,28)", pass["backgroundColor"]!.GetValue<string>());
        Assert.Equal("rgb(255,255,255)", pass["foregroundColor"]!.GetValue<string>());
        Assert.Equal("rgb(255,255,255)", pass["labelColor"]!.GetValue<string>());
        Assert.Equal("205 pts", SingleField(pass, "points")["value"]!.GetValue<string>());
    }

    [Fact]
    [Trait("Category", "MTLevel4")]
    [Trait("Category", "MTLevel6")]
    [Trait("Category", "WalletProductionUpdate")]
    public void Pass_json_uses_renamed_dynamic_next_level_after_glow_to_plata()
    {
        var now = new DateTime(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);
        var customer = NewCustomer(now);
        var card = NewCard(customer.Id, now);
        var snapshot = ProgramConfigSnapshot.FromEntries([]);
        card.EarnPoints(245, TransactionType.Purchase, snapshot, new FixedClock(now));

        var levels = new[]
        {
            new TenantLoyaltyLevelDto(Guid.NewGuid(), "Mist", 0, 1),
            new TenantLoyaltyLevelDto(Guid.NewGuid(), "Plata", 1000, 2),
            new TenantLoyaltyLevelDto(Guid.NewGuid(), "Radiance", 3000, 3)
        };
        var progress = CalculateProgress(245, levels);
        var pass = BuildPassJson(
            card,
            customer,
            progress: new PassProgressValues(
                $"{progress.CurrentLevel.Name} Member \u2728",
                $"{progress.CurrentLevel.Name} \u2728",
                $"{card.CurrentPoints} pts",
                progress.NextLevel!.Name,
                $"{progress.PointsToNextLevel} pts"));

        Assert.Equal("245 pts", SingleField(pass, "points")["value"]!.GetValue<string>());
        Assert.Equal("Mist \u2728", SingleField(pass, "level")["value"]!.GetValue<string>());
        Assert.Equal("Plata", SingleField(pass, "next")["value"]!.GetValue<string>());
        Assert.Equal("755 pts", SingleField(pass, "remaining")["value"]!.GetValue<string>());
        Assert.DoesNotContain("Glow", pass.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "MTLevel4")]
    public void Pass_json_renders_dynamic_max_level_without_legacy_radiance_assumption()
    {
        var now = new DateTime(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);
        var customer = NewCustomer(now);
        var card = NewCard(customer.Id, now);
        var snapshot = ProgramConfigSnapshot.FromEntries([]);
        card.EarnPoints(5200, TransactionType.Purchase, snapshot, new FixedClock(now));

        var pass = BuildPassJson(
            card,
            customer,
            progress: new PassProgressValues("Diamante Member ✨", "Diamante ✨", "5,200 pts", "Máximo ✨", "—"));

        Assert.Equal("Diamante ✨", SingleField(pass, "level")["value"]!.GetValue<string>());
        Assert.Equal("Máximo ✨", SingleField(pass, "next")["value"]!.GetValue<string>());
        Assert.Equal("—", SingleField(pass, "remaining")["value"]!.GetValue<string>());
    }

    private static Customer NewCustomer(DateTime now) =>
        new(
            Guid.NewGuid(),
            Guid.Parse("b1000000-0000-0000-0000-000000000001"),
            "Daniel Chavez",
            "daniel@example.local",
            new DateTime(1990, 1, 1),
            now,
            "6461234567");

    private static LoyaltyCard NewCard(Guid customerId, DateTime now) =>
        new(
            Guid.NewGuid(),
            Guid.Parse("b1000000-0000-0000-0000-000000000001"),
            customerId,
            "KB-LNB7ACG",
            now);

    private static JsonObject BuildPassJson(
        LoyaltyCard card,
        Customer customer,
        WalletNotificationContext? walletContext = null,
        PassProgressValues? progress = null,
        TenantWalletBrandingDto? branding = null)
    {
        var passGeneratorType = typeof(AppDbContext).Assembly
            .GetType("LoyaltyCloud.Infrastructure.Services.PassGeneratorService", throwOnError: true)!;
        var loggerType = typeof(NullLogger<>).MakeGenericType(passGeneratorType);
        var logger = loggerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
            ?? loggerType.GetField("Instance", BindingFlags.Public | BindingFlags.Static)!.GetValue(null);
        var service = Activator.CreateInstance(
            passGeneratorType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                Options.Create(new ApplePassOptions
                {
                    PassTypeIdentifier = "pass.com.kbeautymx.loyalty",
                    TeamIdentifier = "HS2XCFGQ75",
                    WebServiceURL = "https://loyaltycloud-api-894839.azurewebsites.net",
                    OrganizationName = "KBeauty MX",
                    ApnHost = "https://api.push.apple.com"
                }),
                logger
            ],
            culture: null)!;

        var method = passGeneratorType.GetMethod("BuildPassJson", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var progressValue = progress ?? new PassProgressValues(
            $"{card.Level} Member ✨",
            $"{card.Level} ✨",
            $"{card.CurrentPoints} pts",
            "Glow",
            "0 pts");
        var passJson = method.Invoke(
            service,
            [
                card,
                customer,
                walletContext ?? new WalletNotificationContext(null, null, null, null, null, null, null, null, null),
                branding ?? new TenantWalletBrandingDto(
                    card.TenantId,
                    "kbeauty",
                    "KBeauty",
                    "KBeauty MX",
                    "KBeauty Loyalty",
                    "rgb(255,255,255)",
                    "rgb(0,0,0)",
                    "rgb(28,28,28)",
                    "#FFFFFF",
                    null,
                    null,
                    "@kbeauty_mx\n\nkbeautymx.com\n\n+52 646 238 6962",
                    "Cliente K-Beauty",
                    UsesBundledAssetsFallback: true,
                    UsesLegacyContactFallback: false),
                CreatePassProgress(passGeneratorType, progressValue)
            ])!;

        var json = JsonSerializer.Serialize(passJson, new JsonSerializerOptions { PropertyNamingPolicy = null });
        return JsonNode.Parse(json)!.AsObject();
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

    private static object CreatePassProgress(Type passGeneratorType, PassProgressValues progress)
    {
        var progressType = passGeneratorType.GetNestedType("PassProgress", BindingFlags.NonPublic)!;
        return Activator.CreateInstance(
            progressType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                progress.LevelDisplay,
                progress.LevelShortText,
                progress.PointsText,
                progress.NextLevelText,
                progress.RemainingPointsText
            ],
            culture: null)!;
    }

    private static LevelProgressResult CalculateProgress(int rollingPoints, IReadOnlyList<TenantLoyaltyLevelDto> levels)
    {
        var applicationAssembly = typeof(ILevelProgressService).Assembly;
        var calculationType = applicationAssembly.GetType("LoyaltyCloud.Application.Services.LevelCalculationService", throwOnError: true)!;
        var progressType = applicationAssembly.GetType("LoyaltyCloud.Application.Services.LevelProgressService", throwOnError: true)!;
        var calculation = Activator.CreateInstance(calculationType, nonPublic: true)!;
        var progressService = Activator.CreateInstance(
            progressType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [calculation],
            culture: null)!;

        var method = progressType.GetMethod("Calculate", BindingFlags.Instance | BindingFlags.Public)!;
        return (LevelProgressResult)method.Invoke(progressService, [rollingPoints, levels])!;
    }

    private sealed class FixedClock : IDateTimeProvider
    {
        public FixedClock(DateTime utcNow) => UtcNow = utcNow;
        public DateTime UtcNow { get; }
        public DateTime Today => UtcNow.Date;
    }

    private sealed record PassProgressValues(
        string LevelDisplay,
        string LevelShortText,
        string PointsText,
        string NextLevelText,
        string RemainingPointsText);
}
