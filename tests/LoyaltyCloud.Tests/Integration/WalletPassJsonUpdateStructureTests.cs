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
        Assert.DoesNotContain("195 pts", passB.ToJsonString(), StringComparison.Ordinal);
        Assert.DoesNotContain("205 pts", passA.ToJsonString(), StringComparison.Ordinal);
    }

    private static JsonObject BuildPassJson(LoyaltyCard card, Customer customer)
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
        var passJson = method.Invoke(
            service,
            [
                card,
                customer,
                new WalletNotificationContext(null, null, null, null, null, null, null, null),
                new TenantWalletBrandingDto(
                    card.TenantId,
                    "kbeauty",
                    "KBeauty",
                    "KBeauty MX",
                    "KBeauty Loyalty",
                    "rgb(250,248,244)",
                    "rgb(28,28,28)",
                    "rgb(132,124,120)",
                    "@kbeauty_mx\n\nkbeautymx.com\n\n+52 646 238 6962",
                    "Cliente K-Beauty",
                    UsesBundledAssetsFallback: true,
                    UsesLegacyContactFallback: false)
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

    private sealed class FixedClock : IDateTimeProvider
    {
        public FixedClock(DateTime utcNow) => UtcNow = utcNow;
        public DateTime UtcNow { get; }
        public DateTime Today => UtcNow.Date;
    }
}
