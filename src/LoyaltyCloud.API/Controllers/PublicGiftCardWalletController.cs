using LoyaltyCloud.Application.GiftCards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoyaltyCloud.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public/giftcards")]
public sealed class PublicGiftCardWalletController(
    IGiftCardClaimService claims,
    ILogger<PublicGiftCardWalletController> logger) : ControllerBase
{
    [HttpGet("claim/{token}/wallet/google")]
    public async Task<IActionResult> Google(string token, CancellationToken ct)
    {
        try
        {
            var link = await claims.GetGoogleWalletLinkAsync(token, ct);
            return Redirect(link.Url);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex,
                "Google Wallet Gift Card link is unavailable. FailureType={FailureType}",
                ex.GetType().Name);
            return Problem("Google Wallet no está disponible temporalmente.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Google Wallet Gift Card link generation failed. FailureType={FailureType}",
                ex.GetType().Name);
            return Problem("No fue posible generar el enlace de Google Wallet.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
