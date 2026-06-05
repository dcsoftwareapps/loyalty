using KBeauty.Loyalty.Application.Common.Interfaces;
using KBeauty.Loyalty.Application.Devices.Commands.RegisterDevice;
using KBeauty.Loyalty.Application.Devices.Commands.UnregisterDevice;
using KBeauty.Loyalty.Application.Devices.Queries.GetUpdatableSerials;
using KBeauty.Loyalty.Common.Constants;
using KBeauty.Loyalty.Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace KBeauty.Loyalty.API.Controllers;

/// <summary>
/// Endpoints requeridos por Apple Wallet. URLs / status codes / headers
/// están definidos por Apple — no modificar firmas sin revisar contra:
/// <c>https://developer.apple.com/documentation/walletpasses/registering_a_pass_for_update_notifications</c>
/// </summary>
[ApiController]
[Route("v1")]
public sealed class PassesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILoyaltyCardRepository _cards;
    private readonly ICustomerRepository _customers;
    private readonly IPassGeneratorService _passes;
    private readonly IWebHostEnvironment _environment;

    public PassesController(
        ISender sender,
        ILoyaltyCardRepository cards,
        ICustomerRepository customers,
        IPassGeneratorService passes,
        IWebHostEnvironment environment)
    {
        _sender = sender;
        _cards = cards;
        _customers = customers;
        _passes = passes;
        _environment = environment;
    }

    /// <summary>
    /// GET /v1/passes/{passTypeIdentifier}/{serialNumber}
    /// Apple llama esto en cada update — auth ya validó el middleware.
    /// </summary>
    [HttpGet("passes/{passTypeIdentifier}/{serialNumber}")]
    public async Task<IActionResult> GetPass(
        string passTypeIdentifier,
        string serialNumber,
        CancellationToken ct)
    {
        var card = await _cards.GetBySerialNumberAsync(serialNumber, ct);
        if (card is null) return NotFound();

        var customer = await _customers.GetByIdAsync(card.CustomerId, ct);
        if (customer is null) return NotFound();

        // Re-genera siempre (los datos del pase son derivados de card+customer).
        var bytes = await _passes.GeneratePassAsync(card, customer, ct);

        Response.Headers.LastModified = card.LastActivityAt.ToString("R");
        return File(bytes, LoyaltyConstants.ApplePass.ContentType, $"{serialNumber}.pkpass");
    }

    /// <summary>
    /// GET /api/dev/passes/{serialNumber}
    /// Endpoint local para descargar un .pkpass desde navegador/iPhone durante Development.
    /// </summary>
    [HttpGet("~/api/dev/passes/{serialNumber}")]
    public async Task<IActionResult> GetDevelopmentPass(string serialNumber, CancellationToken ct)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        var card = await _cards.GetBySerialNumberAsync(serialNumber, ct);
        if (card is null) return NotFound();

        var customer = await _customers.GetByIdAsync(card.CustomerId, ct);
        if (customer is null) return NotFound();

        var bytes = await _passes.GeneratePassAsync(card, customer, ct);
        return File(bytes, LoyaltyConstants.ApplePass.ContentType, $"{serialNumber}.pkpass");
    }

    /// <summary>
    /// POST /v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}/{serialNumber}
    /// El iPhone registra un dispositivo para recibir pushes de update.
    /// 201 si nuevo, 200 si ya existía.
    /// </summary>
    [HttpPost("devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}/{serialNumber}")]
    public async Task<IActionResult> RegisterDevice(
        string deviceLibraryIdentifier,
        string passTypeIdentifier,
        string serialNumber,
        [FromBody] PushTokenBody body,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body?.PushToken))
            return BadRequest();

        var result = await _sender.Send(
            new RegisterDeviceCommand(deviceLibraryIdentifier, passTypeIdentifier, serialNumber, body.PushToken),
            ct);

        if (result.IsFailure) return NotFound();

        return result.Value.WasNew
            ? StatusCode(StatusCodes.Status201Created)
            : Ok();
    }

    /// <summary>
    /// DELETE /v1/devices/{...}/registrations/{...}/{...}
    /// El usuario quitó el pase de Wallet.
    /// </summary>
    [HttpDelete("devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}/{serialNumber}")]
    public async Task<IActionResult> UnregisterDevice(
        string deviceLibraryIdentifier,
        string passTypeIdentifier,
        string serialNumber,
        CancellationToken ct)
    {
        await _sender.Send(
            new UnregisterDeviceCommand(deviceLibraryIdentifier, passTypeIdentifier, serialNumber),
            ct);
        return Ok();
    }

    /// <summary>
    /// GET /v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}?passesUpdatedSince=...
    /// Apple pregunta qué pases del dispositivo cambiaron desde un tag previo.
    /// El tag es el <c>lastUpdated</c> que devolvemos como Unix seconds.
    /// </summary>
    [HttpGet("devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}")]
    public async Task<IActionResult> GetUpdatableSerials(
        string deviceLibraryIdentifier,
        string passTypeIdentifier,
        [FromQuery] string? passesUpdatedSince,
        CancellationToken ct)
    {
        DateTime? since = null;
        if (long.TryParse(passesUpdatedSince, out var unixSeconds) && unixSeconds > 0)
        {
            since = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
        }

        var result = await _sender.Send(
            new GetUpdatableSerialsQuery(deviceLibraryIdentifier, passTypeIdentifier, since),
            ct);

        if (result.IsFailure || result.Value.SerialNumbers.Count == 0)
            return StatusCode(StatusCodes.Status204NoContent);

        return Ok(new
        {
            serialNumbers = result.Value.SerialNumbers,
            lastUpdated = ((DateTimeOffset)result.Value.LastUpdated).ToUnixTimeSeconds().ToString()
        });
    }

    public sealed record PushTokenBody(string PushToken);
}
