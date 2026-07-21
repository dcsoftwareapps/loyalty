using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Devices.Commands.RegisterDevice;
using LoyaltyCloud.Application.Devices.Commands.UnregisterDevice;
using LoyaltyCloud.Application.Devices.Queries.GetUpdatableSerials;
using LoyaltyCloud.Common.Constants;
using LoyaltyCloud.Domain.Repositories;
using LoyaltyCloud.Infrastructure.Configuration;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LoyaltyCloud.API.Controllers;

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
    private readonly ApplePassOptions _options;
    private readonly ILogger<PassesController> _logger;

    public PassesController(
        ISender sender,
        ILoyaltyCardRepository cards,
        ICustomerRepository customers,
        IPassGeneratorService passes,
        IWebHostEnvironment environment,
        IOptions<ApplePassOptions> options,
        ILogger<PassesController> logger)
    {
        _sender = sender;
        _cards = cards;
        _customers = customers;
        _passes = passes;
        _environment = environment;
        _options = options.Value;
        _logger = logger;
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
        if (!IsConfiguredPassType(passTypeIdentifier))
            return NotFound();

        var card = await _cards.GetBySerialNumberAsync(serialNumber, ct);
        if (card is null) return NotFound();

        var customer = await _customers.GetByIdAsync(card.CustomerId, ct);
        if (customer is null) return NotFound();

        _logger.LogInformation(
            "Apple Wallet GET pass for serial {Serial}; passType={PassType}; level={Level}; lastActivityAt={LastActivityAt:O}.",
            serialNumber,
            passTypeIdentifier,
            card.Level,
            card.LastActivityAt);

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

        _logger.LogInformation(
            "Development pass download for serial {Serial}; level={Level}; lastActivityAt={LastActivityAt}.",
            serialNumber,
            card.Level,
            card.LastActivityAt);

        var bytes = await _passes.GeneratePassAsync(card, customer, ct);
        return File(bytes, LoyaltyConstants.ApplePass.ContentType, $"{serialNumber}.pkpass");
    }

    /// <summary>GET /api/passes/{serialNumber} - descarga publica del .pkpass para alta/reinstalacion.</summary>
    [HttpGet("~/api/passes/{serialNumber}")]
    public async Task<IActionResult> DownloadPass(string serialNumber, CancellationToken ct)
    {
        var card = await _cards.GetBySerialNumberAsync(serialNumber, ct);
        if (card is null) return NotFound();

        var customer = await _customers.GetByIdAsync(card.CustomerId, ct);
        if (customer is null) return NotFound();

        _logger.LogInformation(
            "Public pass download for serial {Serial}; level={Level}; lastActivityAt={LastActivityAt}.",
            serialNumber,
            card.Level,
            card.LastActivityAt);

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
        if (!IsConfiguredPassType(passTypeIdentifier))
            return NotFound();

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
        if (!IsConfiguredPassType(passTypeIdentifier))
            return NotFound();

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
        if (!IsConfiguredPassType(passTypeIdentifier))
            return NotFound();

        DateTime? since = null;
        if (long.TryParse(passesUpdatedSince, out var unixSeconds) && unixSeconds > 0)
        {
            since = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
        }

        _logger.LogInformation(
            "Apple Wallet GET registrations for device {Device} and pass type {PassType}; raw passesUpdatedSince={RawSince}, parsedSince={ParsedSince}.",
            SafeDeviceIdentifier(deviceLibraryIdentifier),
            passTypeIdentifier,
            passesUpdatedSince ?? "null",
            since.HasValue ? since.Value.ToString("O") : "null");

        var result = await _sender.Send(
            new GetUpdatableSerialsQuery(deviceLibraryIdentifier, passTypeIdentifier, since),
            ct);

        if (result.IsFailure || result.Value.SerialNumbers.Count == 0)
        {
            _logger.LogInformation(
                "Apple Wallet GET registrations result for device {Device} and pass type {PassType}: status=204, passesUpdatedSince={Since}, serialNumbers=[].",
                SafeDeviceIdentifier(deviceLibraryIdentifier),
                passTypeIdentifier,
                since.HasValue ? since.Value.ToString("O") : "beginning");
            return StatusCode(StatusCodes.Status204NoContent);
        }

        _logger.LogInformation(
            "Apple Wallet GET registrations result for device {Device} and pass type {PassType}: status=200, serialNumbers=[{SerialNumbers}], lastUpdated={LastUpdated:O}.",
            SafeDeviceIdentifier(deviceLibraryIdentifier),
            passTypeIdentifier,
            string.Join(", ", result.Value.SerialNumbers),
            result.Value.LastUpdated);

        return Ok(new
        {
            serialNumbers = result.Value.SerialNumbers,
            lastUpdated = ((DateTimeOffset)result.Value.LastUpdated).ToUnixTimeSeconds().ToString()
        });
    }

    /// <summary>POST /v1/log - recibe diagnósticos enviados por Apple Wallet.</summary>
    [HttpPost("log")]
    public IActionResult Log([FromBody] AppleLogBody? body)
    {
        var logs = body?.Logs?
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Take(50)
            .ToList() ?? [];

        if (logs.Count == 0)
        {
            _logger.LogDebug("Apple Wallet /v1/log received with no messages.");
            return Ok();
        }

        foreach (var message in logs)
        {
            var safeMessage = message
                .Replace('\r', ' ')
                .Replace('\n', ' ');

            if (safeMessage.Length > 2000)
                safeMessage = safeMessage[..2000];

            _logger.LogWarning("Apple Wallet client log: {WalletMessage}", safeMessage);
        }

        return Ok();
    }

    private bool IsConfiguredPassType(string passTypeIdentifier)
    {
        var isValid = string.Equals(
            passTypeIdentifier,
            _options.PassTypeIdentifier,
            StringComparison.Ordinal);

        if (!isValid)
        {
            _logger.LogWarning(
                "Apple Wallet request used an unknown pass type identifier.");
        }

        return isValid;
    }

    private static string SafeDeviceIdentifier(string value) =>
        value.Length <= 8 ? value : $"{value[..4]}...{value[^4..]}";

    public sealed record PushTokenBody(string PushToken);

    public sealed record AppleLogBody(IReadOnlyList<string>? Logs);
}
