using System.Text.Json.Serialization;
using LoyaltyCloud.API.Auth;
using LoyaltyCloud.Application.Stamps;
using LoyaltyCloud.Common.Results;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoyaltyCloud.API.Controllers;

[ApiController]
[Route("api/stamps")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = CashierAuthDefaults.AuthenticationScheme, Policy = CashierAuthorizationPolicies.CashierOperations)]
public sealed class StampsController(ISender sender) : ControllerBase
{
    [HttpGet("{serialNumber}")]
    public async Task<IActionResult> State(string serialNumber, CancellationToken ct) => Respond(await sender.Send(new GetStampStateQuery(serialNumber), ct));

    [HttpPost("{serialNumber}/earn")]
    public async Task<IActionResult> Earn(string serialNumber, OperationRequest body, CancellationToken ct) =>
        Respond(await sender.Send(new EarnStampCommand(serialNumber, body.IdempotencyKey), ct));

    [HttpPost("{serialNumber}/reverse-last")]
    public async Task<IActionResult> Reverse(string serialNumber, OperationRequest body, CancellationToken ct) =>
        Respond(await sender.Send(new ReverseLastStampCommand(serialNumber, body.IdempotencyKey), ct));

    [HttpPost("{serialNumber}/rewards/{rewardId:guid}/redeem")]
    public async Task<IActionResult> Redeem(string serialNumber, Guid rewardId, OperationRequest body, CancellationToken ct) =>
        Respond(await sender.Send(new RedeemStampRewardCommand(serialNumber, rewardId, body.IdempotencyKey), ct));

    private IActionResult Respond<T>(Result<T> result)
    {
        if (result.IsSuccess) return Ok(result.Value);
        var status = result.Error.StartsWith("Conflict:", StringComparison.Ordinal) ? 409
            : result.Error.StartsWith("Forbidden:", StringComparison.Ordinal) ? 403 : 400;
        return StatusCode(status, new ProblemDetails { Status = status, Title = "Sellos", Detail = result.Error });
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record OperationRequest(string IdempotencyKey);
}
