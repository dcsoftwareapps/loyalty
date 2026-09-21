using LoyaltyCloud.API.Controllers;
using LoyaltyCloud.Application.GiftCards;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LoyaltyCloud.Tests.Integration;

public sealed class PublicGiftCardWalletControllerTests
{
    [Fact]
    [Trait("Category", "GiftCards")]
    public async Task Google_redirects_to_generated_save_link()
    {
        var claims = new StubClaims
        {
            Result = new GiftCardWalletLinkDto(
                LoyaltyCloud.Domain.Enums.GiftCardWalletProvider.Google,
                "https://pay.google.com/gp/v/save/signed-token",
                "issuer.class", "issuer.object")
        };
        var controller = Controller(claims);

        var result = await controller.Google("claim-token", CancellationToken.None);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("https://pay.google.com/gp/v/save/signed-token", redirect.Url);
        Assert.Equal("claim-token", claims.Token);
    }

    [Theory]
    [Trait("Category", "GiftCards")]
    [InlineData(typeof(KeyNotFoundException), StatusCodes.Status404NotFound)]
    [InlineData(typeof(InvalidOperationException), StatusCodes.Status503ServiceUnavailable)]
    [InlineData(typeof(HttpRequestException), StatusCodes.Status502BadGateway)]
    public async Task Google_maps_failures_without_exposing_provider_details(Type exceptionType, int expectedStatus)
    {
        var claims = new StubClaims
        {
            Failure = (Exception)Activator.CreateInstance(exceptionType, "sensitive provider detail")!
        };
        var controller = Controller(claims);

        var result = await controller.Google("claim-token", CancellationToken.None);

        if (expectedStatus == StatusCodes.Status404NotFound)
        {
            Assert.IsType<NotFoundResult>(result);
            return;
        }

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(expectedStatus, problem.StatusCode);
        Assert.DoesNotContain("sensitive provider detail", problem.Value?.ToString());
    }

    private static PublicGiftCardWalletController Controller(IGiftCardClaimService claims) =>
        new(claims, NullLogger<PublicGiftCardWalletController>.Instance);

    private sealed class StubClaims : IGiftCardClaimService
    {
        public GiftCardWalletLinkDto? Result { get; init; }
        public Exception? Failure { get; init; }
        public string? Token { get; private set; }

        public Task<GiftCardWalletLinkDto> GetGoogleWalletLinkAsync(string claimToken, CancellationToken ct = default)
        {
            Token = claimToken;
            return Failure is null ? Task.FromResult(Result!) : Task.FromException<GiftCardWalletLinkDto>(Failure);
        }

        public Task<GiftCardClaimDto?> GetAsync(string claimToken, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<GiftCardApplePassResult> GetApplePassAsync(string claimToken, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
