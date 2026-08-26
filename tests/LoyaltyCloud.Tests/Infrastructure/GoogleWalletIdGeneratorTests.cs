using LoyaltyCloud.Infrastructure.Configuration;
using LoyaltyCloud.Infrastructure.Services.GoogleWallet;
using Xunit;

namespace LoyaltyCloud.Tests.Infrastructure;

public sealed class GoogleWalletIdGeneratorTests
{
    private static readonly Guid TenantId = Guid.Parse("b1000000-0000-0000-0000-000000000001");

    [Fact]
    public void BuildClassId_ShouldIncludeIssuerAndSanitizedSuffix()
    {
        var generator = new GoogleWalletIdGenerator();
        var options = new GoogleWalletOptions
        {
            IssuerId = "Issuer_123",
            ClassSuffix = "KBeauty Loyalty"
        };

        var id = generator.BuildClassId(options, TenantId);

        Assert.Equal($"issuer_123.kbeauty_loyalty-{TenantId:N}", id);
    }

    [Fact]
    public void BuildObjectId_ShouldBeDeterministicAndNotUseMutableName()
    {
        var generator = new GoogleWalletIdGenerator();
        var options = new GoogleWalletOptions
        {
            IssuerId = "issuer",
            ObjectIdPrefix = "member"
        };

        var first = generator.BuildObjectId(options, TenantId, "KB-Test 001");
        var second = generator.BuildObjectId(options, TenantId, "KB-Test 001");

        Assert.Equal("issuer.member-b10000000000-kb-test_001", first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void BuildObjectId_ShouldDifferentiateMembers()
    {
        var generator = new GoogleWalletIdGenerator();
        var options = new GoogleWalletOptions
        {
            IssuerId = "issuer",
            ObjectIdPrefix = "member"
        };

        var first = generator.BuildObjectId(options, TenantId, "KB-001");
        var second = generator.BuildObjectId(options, TenantId, "KB-002");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void BuildClassId_ShouldThrow_WhenIssuerMissing()
    {
        var generator = new GoogleWalletIdGenerator();
        var options = new GoogleWalletOptions { IssuerId = "" };

        Assert.Throws<InvalidOperationException>(() => generator.BuildClassId(options, TenantId));
    }
}

