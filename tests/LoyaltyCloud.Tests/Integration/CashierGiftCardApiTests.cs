using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LoyaltyCloud.API.Auth;
using LoyaltyCloud.API.Controllers;
using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.GiftCards;
using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using LoyaltyCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

[Trait("Category", "CashierGiftCards")]
public sealed class CashierGiftCardApiTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Theory]
    [InlineData(TenantUserRole.Cashier)]
    [InlineData(TenantUserRole.Admin)]
    public async Task Authorized_roles_lookup_redeem_and_replay_without_exposing_internal_data(TenantUserRole role)
    {
        var actor = await SeedAsync(role);
        using var client = Client(actor.Token);
        var lookup = await client.PostAsJsonAsync("/api/giftcards/lookup", new { code = "  " + actor.Code.ToLowerInvariant() + "  " });
        Assert.Equal(HttpStatusCode.OK, lookup.StatusCode);
        var json = await lookup.Content.ReadAsStringAsync();
        Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("claim", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("transactions", json, StringComparison.OrdinalIgnoreCase);
        var summary = await lookup.Content.ReadFromJsonAsync<GiftCardsController.CardSummary>();
        Assert.Equal(100m, summary!.RemainingBalance);
        Assert.True(summary.AllowPartialRedemption);
        var key = Guid.NewGuid().ToString("N");
        for (var i = 0; i < 2; i++)
        {
            var response = await client.PostAsJsonAsync($"/api/giftcards/{actor.Code}/redeem", new { amount = 25m, idempotencyKey = key });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<GiftCardsController.RedeemResponse>();
            Assert.Equal(25m, body!.RedeemedAmount);
            Assert.Equal(75m, body.Card.RemainingBalance);
            Assert.Equal(i == 1, body.WasIdempotent);
        }
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(actor.TenantId, actor.Slug);
        var transactions = await scope.ServiceProvider.GetRequiredService<AppDbContext>().GiftCardTransactions.ToListAsync();
        Assert.Equal(actor.UserId, Assert.Single(transactions).PerformedByUserId);
    }

    [Fact]
    public async Task Cashier_can_issue_gift_card_and_replay_same_idempotency_key()
    {
        var actor = await SeedAsync();
        using var client = Client(actor.Token);

        var options = await client.GetAsync("/api/giftcards/issue/options");
        Assert.Equal(HttpStatusCode.OK, options.StatusCode);
        var parsedOptions = await options.Content.ReadFromJsonAsync<GiftCardsController.IssueOptionsResponse>();
        Assert.True(parsedOptions!.AllowCustomAmount);

        var key = Guid.NewGuid().ToString("N");
        var payload = new { amount = 150m, recipientName = "Comprador STG", recipientEmail = "buyer@example.test", senderName = "Daniel", personalMessage = "¡Feliz cumpleaños!", idempotencyKey = key };
        var first = await client.PostAsJsonAsync("/api/giftcards/issue", payload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var issued = await first.Content.ReadFromJsonAsync<GiftCardsController.IssueResponse>();
        Assert.NotNull(issued);
        Assert.StartsWith("GC-", issued!.Card.Code);
        Assert.Equal(150m, issued.Card.InitialBalance);
        Assert.Equal(150m, issued.Card.RemainingBalance);
        Assert.Equal("Comprador STG", issued.Card.RecipientName);
        Assert.Equal("Daniel", issued.Card.SenderName);
        Assert.Equal("¡Feliz cumpleaños!", issued.Card.PersonalMessage);

        var replay = await client.PostAsJsonAsync("/api/giftcards/issue", payload);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayed = await replay.Content.ReadFromJsonAsync<GiftCardsController.IssueResponse>();
        Assert.Equal(issued.Card.Code, replayed!.Card.Code);

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(actor.TenantId, actor.Slug);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await db.GiftCards.CountAsync());
        var issuedTransactions = await db.GiftCardTransactions.Where(x => x.Type == GiftCardTransactionType.Issued).ToListAsync();
        var transaction = Assert.Single(issuedTransactions);
        Assert.Equal(key, transaction.IdempotencyKey);
        Assert.Equal(actor.UserId, transaction.PerformedByUserId);
        var card = await db.GiftCards.SingleAsync(x => x.PublicCode == issued.Card.Code);
        Assert.Equal("Comprador STG", card.RecipientName);
        Assert.Equal("Daniel", card.SenderName);
        Assert.Equal("¡Feliz cumpleaños!", card.PersonalMessage);
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("recipient")]
    [InlineData("sender")]
    [InlineData("message")]
    public async Task Issue_same_key_with_different_payload_is_conflict_without_second_card(string changedField)
    {
        var actor = await SeedAsync();
        using var client = Client(actor.Token);
        var key = Guid.NewGuid().ToString("N");
        var original = new { amount = 150m, recipientName = "Cliente", senderName = "Daniel", personalMessage = "Abrazo", idempotencyKey = key };

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/giftcards/issue", original)).StatusCode);
        var conflictPayload = changedField switch
        {
            "amount" => new { amount = 151m, recipientName = "Cliente", senderName = "Daniel", personalMessage = "Abrazo", idempotencyKey = key },
            "recipient" => new { amount = 150m, recipientName = "Otra persona", senderName = "Daniel", personalMessage = "Abrazo", idempotencyKey = key },
            "sender" => new { amount = 150m, recipientName = "Cliente", senderName = "Ana", personalMessage = "Abrazo", idempotencyKey = key },
            _ => new { amount = 150m, recipientName = "Cliente", senderName = "Daniel", personalMessage = "Otro mensaje", idempotencyKey = key }
        };
        var conflict = await client.PostAsJsonAsync("/api/giftcards/issue", conflictPayload);

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Contains("IdempotencyConflict", await conflict.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(actor.TenantId, actor.Slug);
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<AppDbContext>().GiftCards.CountAsync());
    }

    [Fact]
    public async Task Disabled_gift_card_module_rejects_issue_options_and_issue()
    {
        var actor = await SeedAsync();
        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(actor.TenantId, actor.Slug);
            var config = await scope.ServiceProvider.GetRequiredService<AppDbContext>().GiftCardConfigurations.SingleAsync();
            config.SetEnabled(false, DateTime.UtcNow);
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync();
        }

        using var client = Client(actor.Token);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/giftcards/issue/options")).StatusCode);
        var issue = await client.PostAsJsonAsync("/api/giftcards/issue",
            new { amount = 100m, recipientName = "Cliente", idempotencyKey = Guid.NewGuid().ToString("N") });
        Assert.Equal(HttpStatusCode.Forbidden, issue.StatusCode);
    }

    [Fact]
    public async Task Issued_gift_card_is_scoped_to_authenticated_tenant()
    {
        var a = await SeedAsync();
        var b = await SeedAsync();
        using var clientA = Client(a.Token);
        var issue = await clientA.PostAsJsonAsync("/api/giftcards/issue",
            new { amount = 120m, recipientName = "Tenant A", idempotencyKey = Guid.NewGuid().ToString("N") });
        var issued = await issue.Content.ReadFromJsonAsync<GiftCardsController.IssueResponse>();

        using var clientB = Client(b.Token);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.PostAsJsonAsync("/api/giftcards/lookup",
            new { code = issued!.Card.Code })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await clientA.PostAsJsonAsync("/api/giftcards/lookup",
            new { code = issued.Card.Code })).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid-token")]
    public async Task Missing_or_invalid_bearer_is_rejected(string? token)
    {
        using var client = Client(token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/giftcards/lookup", new { code = "GC-AAAA-BBBB-CCCC" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/giftcards/GC-AAAA-BBBB-CCCC/redeem", new { amount = 1, idempotencyKey = "key" })).StatusCode);
    }

    [Fact]
    public async Task Claims_and_request_headers_cannot_cross_tenant_boundaries()
    {
        var a = await SeedAsync(); var b = await SeedAsync();
        using var client = Client(a.Token);
        client.DefaultRequestHeaders.Add("X-Tenant-Slug", b.Slug);
        client.DefaultRequestHeaders.Add("X-Operator-Id", b.UserId.ToString());
        foreach (var body in new object[] { new { code = b.Code, tenantSlug = b.Slug }, new { claimToken = b.ClaimToken } })
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/giftcards/lookup", body)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/giftcards/{b.Code}/redeem",
            new { amount = 10, idempotencyKey = "cross-tenant", tenantId = b.TenantId, operatorId = b.UserId })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/giftcards/{a.Code}/redeem",
            new { amount = 10, idempotencyKey = "own-tenant", tenantId = b.TenantId, operatorId = b.UserId })).StatusCode);
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(a.TenantId, a.Slug);
        Assert.Equal(a.UserId, (await scope.ServiceProvider.GetRequiredService<AppDbContext>().GiftCardTransactions.SingleAsync()).PerformedByUserId);
    }

    [Fact]
    public async Task Claim_lookup_is_tenant_scoped_and_does_not_redeem()
    {
        var a = await SeedAsync(); using var client = Client(a.Token);
        var response = await client.PostAsJsonAsync("/api/giftcards/lookup", new { claimToken = a.ClaimToken });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(100m, (await response.Content.ReadFromJsonAsync<GiftCardsController.CardSummary>())!.RemainingBalance);
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(a.TenantId, a.Slug);
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<AppDbContext>().GiftCardTransactions.ToListAsync());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"code\":\"GC-AAAA-BBBB-CCCC\",\"claimToken\":\"token\"}")]
    [InlineData("{\"claimToken\":\"https://example.test/giftcards/claim/token\"}")]
    public async Task Lookup_rejects_invalid_identifier_combinations(string json)
    {
        var a = await SeedAsync(); using var client = Client(a.Token);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/giftcards/lookup", new StringContent(json, System.Text.Encoding.UTF8, "application/json"))).StatusCode);
    }

    [Fact]
    public async Task Same_key_different_amount_is_conflict_and_full_redemption_updates_status()
    {
        var a = await SeedAsync(); using var client = Client(a.Token);
        var path = $"/api/giftcards/{a.Code}/redeem";
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(path, new { amount = 40, idempotencyKey = "one" })).StatusCode);
        var conflict = await client.PostAsJsonAsync(path, new { amount = 41, idempotencyKey = "one" });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Contains("IdempotencyConflict", await conflict.Content.ReadAsStringAsync());
        var full = await client.PostAsJsonAsync(path, new { amount = 60, idempotencyKey = "two" });
        var result = await full.Content.ReadFromJsonAsync<GiftCardsController.RedeemResponse>();
        Assert.Equal("FullyRedeemed", result!.Card.Status);
        Assert.Equal(0m, result.Card.RemainingBalance);
        var replay = await client.PostAsJsonAsync(path, new { amount = 40, idempotencyKey = "one" });
        var prior = await replay.Content.ReadFromJsonAsync<GiftCardsController.RedeemResponse>();
        Assert.Equal(40m, prior!.RedeemedAmount);
        Assert.Equal(0m, prior.Card.RemainingBalance);
    }

    [Theory]
    [InlineData(0, 400, "InvalidInput")]
    [InlineData(101, 422, "InsufficientBalance")]
    public async Task Invalid_amounts_are_distinguished(decimal amount, int status, string error)
    {
        var a = await SeedAsync(); using var client = Client(a.Token);
        var response = await client.PostAsJsonAsync($"/api/giftcards/{a.Code}/redeem", new { amount, idempotencyKey = "invalid" });
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Contains(error, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("disabled", 403, "Unavailable")]
    [InlineData("expired", 422, "Expired")]
    [InlineData("cancelled", 422, "Inactive")]
    [InlineData("full-only", 422, "PartialRedemptionNotAllowed")]
    public async Task Server_enforces_card_status_and_tenant_settings(string state, int status, string error)
    {
        var a = await SeedAsync();
        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(a.TenantId, a.Slug);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var config = await db.GiftCardConfigurations.SingleAsync();
            var card = await db.GiftCards.SingleAsync();
            if (state == "disabled") config.SetEnabled(false, DateTime.UtcNow);
            if (state == "full-only") config.Update(true, true, false, true, GiftCardExpirationMode.Never, null,
                "MXN", "Gift Card", "#312ee9", "#FFFFFF", null, null, null, null, DateTime.UtcNow);
            if (state == "expired") db.Entry(card).Property(nameof(GiftCard.ExpiresAtUtc)).CurrentValue = DateTime.UtcNow.AddDays(-1);
            if (state == "cancelled") card.Cancel(DateTime.UtcNow);
            await db.SaveChangesAsync();
        }
        using var client = Client(a.Token);
        var response = await client.PostAsJsonAsync($"/api/giftcards/{a.Code}/redeem", new { amount = 10, idempotencyKey = "status" });
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Contains(error, await response.Content.ReadAsStringAsync());
        if (state == "disabled")
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/giftcards/lookup", new { code = a.Code })).StatusCode);
    }

    [Fact]
    public async Task Same_key_different_card_is_conflict_without_another_debit()
    {
        var a = await SeedAsync();
        const string other = "GC-1111-2222-3333";
        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(a.TenantId, a.Slug);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Add(new GiftCard(Guid.NewGuid(), a.TenantId, other, GiftCard.HashClaimToken(Guid.NewGuid().ToString()),
                100, "MXN", null, "Other", null, null, null, null, GiftCardSource.Manual, a.UserId, DateTime.UtcNow, null));
            await db.SaveChangesAsync();
        }
        using var client = Client(a.Token);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/giftcards/{a.Code}/redeem", new { amount = 10, idempotencyKey = "same" })).StatusCode);
        var response = await client.PostAsJsonAsync($"/api/giftcards/{other}/redeem", new { amount = 10, idempotencyKey = "same" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("IdempotencyConflict", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Transport_rejects_missing_key_excess_precision_and_oversized_reference()
    {
        var a = await SeedAsync(); using var client = Client(a.Token);
        foreach (var body in new object[] { new { amount = 1 }, new { amount = 1.001m, idempotencyKey = "precision" },
            new { amount = 1, idempotencyKey = "reference", reference = new string('x', 201) } })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/giftcards/{a.Code}/redeem", body)).StatusCode);
    }

    private HttpClient Client(string? token)
    {
        var client = factory.CreateClient();
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Actor> SeedAsync(TenantUserRole role = TenantUserRole.Cashier)
    {
        await factory.EnsureDatabaseCreatedAsync();
        using var scope = factory.Services.CreateScope();
        var tenantId = Guid.NewGuid(); var userId = Guid.NewGuid(); var now = DateTime.UtcNow;
        var slug = "gift-" + tenantId.ToString("N")[..12];
        scope.ServiceProvider.GetRequiredService<IMutableTenantContext>().SetTenant(tenantId, slug);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = new Tenant(tenantId, slug, "Gift test", "UTC", now);
        var user = new TenantAdminUser(userId, tenantId, "cashier", "unused-test-hash", now, role: role);
        var config = new GiftCardConfiguration(Guid.NewGuid(), tenantId, now);
        config.Update(true, true, true, true, GiftCardExpirationMode.Never, null, "MXN", "Gift Card", "#312ee9", "#FFFFFF", null, null, null, null, now);
        var raw = Guid.NewGuid().ToString("N").ToUpperInvariant();
        var code = $"GC-{raw[..4]}-{raw[4..8]}-{raw[8..12]}";
        var claimToken = Guid.NewGuid().ToString("N");
        db.Add(tenant); db.Add(new TenantSubscription(tenantId, TenantSubscriptionStatus.Active, "test", paidThroughUtc: now.AddYears(1)));
        db.Add(user); db.Add(config);
        db.Add(new GiftCard(Guid.NewGuid(), tenantId, code, GiftCard.HashClaimToken(claimToken), 100m, "MXN", null, "Recipient", "private@example.test", null, null, "private", GiftCardSource.Manual, userId, now, null));
        await db.SaveChangesAsync();
        var token = scope.ServiceProvider.GetRequiredService<CashierAccessTokenService>().CreateToken(tenant, user).AccessToken;
        return new(tenantId, userId, slug, code, claimToken, token);
    }

    private sealed record Actor(Guid TenantId, Guid UserId, string Slug, string Code, string ClaimToken, string Token);
}
