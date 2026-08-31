using LoyaltyCloud.Domain.Entities;
using LoyaltyCloud.Domain.Enums;
using Xunit;

namespace LoyaltyCloud.Tests.Domain;

public sealed class GiftCardTests
{
    private static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid UserId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly DateTime Now = new(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Redeem_AllowsPartialAndMaintainsInvariant()
    {
        var card = NewCard(500);
        var result = card.Redeem(125.50m, true, Now.AddMinutes(1));
        Assert.Equal(500, result.Before);
        Assert.Equal(374.50m, result.After);
        Assert.Equal(GiftCardStatus.Active, card.Status);
    }

    [Fact]
    public void Redeem_RejectsOverdraft()
    {
        var card = NewCard(100);
        Assert.Throws<InvalidOperationException>(() => card.Redeem(100.01m, true, Now));
        Assert.Equal(100, card.CurrentBalance);
    }

    [Fact]
    public void Redeem_RequiresFullBalanceWhenPartialDisabled()
    {
        var card = NewCard(100);
        Assert.Throws<InvalidOperationException>(() => card.Redeem(50, false, Now));
        card.Redeem(100, false, Now);
        Assert.Equal(GiftCardStatus.FullyRedeemed, card.Status);
        Assert.Equal(0, card.CurrentBalance);
    }

    [Fact]
    public void Adjust_NeverAllowsNegativeBalance()
    {
        var card = NewCard(100);
        Assert.Throws<InvalidOperationException>(() => card.Adjust(-101, Now));
        Assert.Equal(100, card.CurrentBalance);
    }

    [Fact]
    public void Expiration_PreventsLaterRedemption()
    {
        var card = NewCard(100, Now.AddDays(1));
        Assert.Equal(100, card.EvaluateExpiration(Now.AddDays(2)));
        Assert.Equal(GiftCardStatus.Expired, card.Status);
        Assert.Throws<InvalidOperationException>(() => card.Redeem(10, true, Now.AddDays(2)));
    }

    [Fact]
    public void Cancel_RevokesClaimWithoutErasingBalance()
    {
        var card = NewCard(100);
        Assert.Equal(100, card.Cancel(Now));
        Assert.True(card.ClaimRevoked);
        Assert.Equal(GiftCardStatus.Cancelled, card.Status);
        Assert.Equal(100, card.CurrentBalance);
    }

    [Fact]
    public void ClaimHash_IsDeterministicAndDoesNotExposeToken()
    {
        var hash = GiftCard.HashClaimToken("secret-claim-token");
        Assert.Equal(hash, GiftCard.HashClaimToken("secret-claim-token"));
        Assert.DoesNotContain("secret-claim-token", hash, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(64, hash.Length);
    }

    private static GiftCard NewCard(decimal value, DateTime? expires = null) =>
        new(Guid.NewGuid(), TenantId, "GC-AAAA-BBBB-CCCC", GiftCard.HashClaimToken("claim"), value, "MXN", null, "Cliente", null, null, null, null, GiftCardSource.Manual, UserId, Now, expires);
}
