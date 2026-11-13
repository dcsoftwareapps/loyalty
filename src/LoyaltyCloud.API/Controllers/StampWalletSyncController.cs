using LoyaltyCloud.Application.Common.Interfaces;
using LoyaltyCloud.Application.Common.Wallet;
using Microsoft.AspNetCore.Mvc;

namespace LoyaltyCloud.API.Controllers;

// Admin calls this after committing a stamp operation. The middleware verifies
// the signed request and establishes the tenant context before this action runs.
[ApiController]
[Route("api/internal/stamps/google")]
public sealed class StampWalletSyncController(IGoogleStampWalletService google) : ControllerBase
{
    [HttpPost("{serialNumber}/sync")]
    [ProducesResponseType(typeof(GoogleStampWalletSyncResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Synchronize(string serialNumber, CancellationToken ct) =>
        Ok(new GoogleStampWalletSyncResponse(await google.SynchronizeExistingAsync(serialNumber, ct)));
}
