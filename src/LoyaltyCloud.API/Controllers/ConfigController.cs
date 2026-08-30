using LoyaltyCloud.Application.Config.Commands.UpdateProgramConfig;
using LoyaltyCloud.Application.Config.Queries.GetProgramConfig;
using LoyaltyCloud.Application.Branding.Commands.RemoveTenantWalletLogo;
using LoyaltyCloud.Application.Branding.Commands.UpdateWalletCardBranding;
using LoyaltyCloud.Application.Branding.Commands.UploadTenantWalletLogo;
using LoyaltyCloud.Application.Common.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LoyaltyCloud.API.Controllers;

[ApiController]
[Route("api/config")]
[Produces("application/json")]
public sealed class ConfigController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ITenantBrandingReadService _brandingRead;

    public ConfigController(ISender sender, ITenantBrandingReadService brandingRead)
    {
        _sender = sender;
        _brandingRead = brandingRead;
    }

    /// <summary>GET /api/config — todas las reglas vigentes.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await _sender.Send(new GetProgramConfigQuery(), ct);
        return Ok(result.Value);
    }

    /// <summary>PUT /api/config — actualiza una o más reglas. Auditado.</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        [FromBody] UpdateConfigRequest body,
        [FromHeader(Name = "X-Operator-Id")] string? operatorId,
        CancellationToken ct)
    {
        var result = await _sender.Send(
            new UpdateProgramConfigCommand(body.Entries, operatorId ?? "api"),
            ct);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Configuración", Detail = result.Error });

        return NoContent();
    }

    /// <summary>PUT /api/config/wallet-branding - actualiza el branding visual del pass Apple Wallet.</summary>
    [HttpPut("wallet-branding")]
    [ProducesResponseType(typeof(TenantBrandingInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateWalletBranding(
        [FromBody] WalletBrandingRequest body,
        CancellationToken ct)
    {
        var result = await _sender.Send(
            new UpdateWalletCardBrandingCommand(
                body.WalletBackgroundColor,
                body.WalletLogoScalePercent),
            ct);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Tarjeta digital", Detail = result.Error });

        return Ok(result.Value);
    }

    /// <summary>POST /api/config/wallet-branding/logo - sube un logo específico para Apple Wallet.</summary>
    [HttpPost("wallet-branding/logo")]
    [ProducesResponseType(typeof(TenantBrandingInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadWalletLogo(
        [FromBody] WalletLogoUploadRequest body,
        CancellationToken ct)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(body.ContentBase64);
        }
        catch (FormatException)
        {
            return BadRequest(new ProblemDetails { Title = "Logo de tarjeta", Detail = "El archivo no tiene un formato valido." });
        }

        await using var stream = new MemoryStream(bytes);
        var result = await _sender.Send(
            new UploadTenantWalletLogoCommand(
                body.FileName,
                body.ContentType,
                stream,
                body.ContentLength),
            ct);

        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Logo de tarjeta", Detail = string.Join(" ", result.Errors) });

        return Ok(await _brandingRead.GetCurrentAsync(ct));
    }

    /// <summary>DELETE /api/config/wallet-branding/logo - elimina el logo específico para Apple Wallet.</summary>
    [HttpDelete("wallet-branding/logo")]
    [ProducesResponseType(typeof(TenantBrandingInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveWalletLogo(CancellationToken ct)
    {
        var result = await _sender.Send(new RemoveTenantWalletLogoCommand(), ct);
        if (result.IsFailure)
            return BadRequest(new ProblemDetails { Title = "Logo de tarjeta", Detail = string.Join(" ", result.Errors) });

        return Ok(await _brandingRead.GetCurrentAsync(ct));
    }

    public sealed record UpdateConfigRequest(IReadOnlyList<ConfigEntry> Entries);

    public sealed record WalletBrandingRequest(
        string? WalletBackgroundColor,
        int? WalletLogoScalePercent);

    public sealed record WalletLogoUploadRequest(
        string FileName,
        string ContentType,
        string ContentBase64,
        long ContentLength);
}
