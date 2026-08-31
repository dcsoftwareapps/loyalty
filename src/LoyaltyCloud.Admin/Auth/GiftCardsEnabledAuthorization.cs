using LoyaltyCloud.Application.GiftCards;
using Microsoft.AspNetCore.Authorization;

namespace LoyaltyCloud.Admin.Auth;

public static class GiftCardsAuthorization
{
    public const string Policy = "GiftCardsEnabled";
}

public sealed class GiftCardsEnabledRequirement : IAuthorizationRequirement;

public sealed class GiftCardsEnabledHandler(IGiftCardService giftCards)
    : AuthorizationHandler<GiftCardsEnabledRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        GiftCardsEnabledRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && await giftCards.IsEnabledAsync())
        {
            context.Succeed(requirement);
        }
    }
}
