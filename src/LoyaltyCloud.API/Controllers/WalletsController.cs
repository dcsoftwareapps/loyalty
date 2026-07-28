using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Common.Wallet;
using LoyaltyCloud.Application.Wallets.Commands.CreateGoogleWalletSaveLink;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LoyaltyCloud.API.Controllers;

[ApiController]
[Route("api/customers/{serialNumber}/wallets/google")]
[Produces("application/json")]
public sealed class WalletsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IWalletTenantContextResolver _walletTenantResolver;

    public WalletsController(
        ISender sender,
        IWalletTenantContextResolver walletTenantResolver)
    {
        _sender = sender;
        _walletTenantResolver = walletTenantResolver;
    }

    /// <summary>
    /// POST /api/customers/{serialNumber}/wallets/google/save-link
    /// Creates or updates the Google Wallet LoyaltyObject and returns a Save to Google Wallet URL.
    /// </summary>
    [HttpPost("save-link")]
    [ProducesResponseType(typeof(GoogleWalletSaveLinkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateSaveLink(string serialNumber, CancellationToken ct)
    {
        var tenant = await _walletTenantResolver.ResolveAndSetTenantAsync(
            serialNumber,
            requireOperational: true,
            ct);
        if (tenant is null)
            return NotFound(new ProblemDetails { Title = "Google Wallet", Detail = "No se encontro la tarjeta." });
        if (!tenant.IsOperational)
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Google Wallet",
                Detail = "El programa de lealtad no esta disponible temporalmente."
            });

        var result = await _sender.Send(new CreateGoogleWalletSaveLinkCommand(serialNumber), ct);

        if (result.IsSuccess)
            return Ok(result.Value);

        var detail = result.Error;
        if (detail.Contains("No se encontro", StringComparison.OrdinalIgnoreCase))
            return NotFound(new ProblemDetails { Title = "Google Wallet", Detail = detail });

        return BadRequest(new ProblemDetails { Title = "Google Wallet", Detail = detail });
    }
}

